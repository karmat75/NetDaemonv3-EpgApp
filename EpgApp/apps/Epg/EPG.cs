using EpgApp.apps.Epg.Models;
using EpgApp.apps.Epg.Services;
using Microsoft.Extensions.Hosting;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace EpgApp.apps.Epg
{
    [NetDaemonApp]
    public class Epg : IAsyncInitializable, IAsyncDisposable
    {
        private readonly ILogger<Epg> _logger;
        private readonly IAppConfig<Config> _config;
        private readonly IMqttEntityManager _entityManager;
        private readonly IScheduler _scheduler;
        private readonly IHaContext _haContext;
        private readonly IHostApplicationLifetime _liveTime;

        private List<DataProvider> _dataProviders;
        private int _refreshrateInSeconds;
        private IEnumerable<string> _defaultGuideRefreshTimes;

        public Epg(
            ILogger<Epg> appLogger, 
            IAppConfig<Config> appConfig,
            IMqttEntityManager mqttEntityManager,
            IScheduler scheduler,
            IHaContext haContext,
            IHostApplicationLifetime liveTime)
        {
            _logger = appLogger;
            _config = appConfig;
            _entityManager = mqttEntityManager;
            _scheduler = scheduler;
            _haContext = haContext;
            _liveTime = liveTime;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            InitialiseDataproviderList();
            SetRefreshrateInSeconds();
            InitialisedefaultGuideRefreshTimes();

            if(_config.Value.PrintChannelsAndExit != null && _config.Value.PrintChannelsAndExit.Value)
            {
                PrintDataProviderChannelList();
                Exit();
            }

            if(_config.Value.CleanupSensorsOnStartup != null && _config.Value.CleanupSensorsOnStartup.Value)
            {
                _logger.LogInformation("Sensor cleanup on start is enabled. All sensors will be removed and recreated if nessesary.");
                await RemoveAllSensorsOnStartup();
            }

            if(_config.Value.OnlyCleanupAndEnd == null || !_config.Value.OnlyCleanupAndEnd.Value)
            {
                await RunApplicationAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("Application is configured to only cleanup sensors and end.");
                Exit();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await SetAvailabilityForAllSensorsToDown();
            return;
        }

        private async Task RunApplicationAsync(CancellationToken cancellationToken)
        {
            LoadDataproviderList();

            foreach(var dataProvider in _dataProviders)
            {
                if(dataProvider.Fullname == null || string.IsNullOrEmpty(dataProvider.Fullname))
                {
                    _logger.LogError("EPG data provider fullname can not be null or empty.");
                    continue;
                }

                if(dataProvider.Stations == null || !dataProvider.Stations.Any())
                {
                    _logger.LogError($"EPG data provider {dataProvider.Fullname} dosn't contain any station");
                    continue;
                }

                var epgService = CreateDataProviderService(dataProvider.Fullname);

                if(epgService == null)
                {
                    _logger.LogError($"Could not create instance of EPG data provider '{dataProvider.Fullname}'");
                    continue;
                }

                if(dataProvider.RefreshTimes == null || !dataProvider.RefreshTimes.Any())
                {
                    dataProvider.RefreshTimes = _defaultGuideRefreshTimes;
                }

                foreach(var station in dataProvider.Stations)
                {
                    var stationGuideArguments = new StationGuideDTO
                    {
                        Logger = _logger,
                        DataProviderService = epgService,
                        Station = station,
                        MqttEntityManager = _entityManager,
                        Scheduler = _scheduler,
                        GuideRefreshTimes = dataProvider.RefreshTimes,
                        RefreshrateInSeconds = _refreshrateInSeconds,
                        HomeAssistantContext = _haContext,
                        Config = _config,
                    };

                    var stationGuide = new StationGuide(stationGuideArguments);
                    await stationGuide.InitializeAsync();

                    _logger.LogInformation($"{dataProvider.Fullname} - {station} initialised.");
                }
            }

        }

        private void LoadDataproviderList()
        {
            if (_config.Value.Dataproviders != null && _config.Value.Dataproviders.Any())
            {
                _dataProviders = _config.Value.Dataproviders.ToList();
            }
            else
            {
                _dataProviders = new List<DataProvider>();
            }
        }

        private IDataProviderService? CreateDataProviderService(string fullname)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            Type? dataProviderServiceType = assembly.GetType(fullname);
            if (dataProviderServiceType == null)
            {
                return null;
            }

            return Activator.CreateInstance(dataProviderServiceType, _logger) as IDataProviderService;
        }

        #region Init functions
        private void InitialiseDataproviderList()
        {
            _dataProviders = new List<DataProvider>();
        }

        private void SetRefreshrateInSeconds()
        {
            int defaultRefreshrateInSeconds = 30;
            if (_config.Value.RefreshrateInSeconds.HasValue)
            {
                _refreshrateInSeconds = _config.Value.RefreshrateInSeconds.Value;
            }
            else
            {
                _refreshrateInSeconds = defaultRefreshrateInSeconds;
            }
        }

        private void InitialisedefaultGuideRefreshTimes()
        {
            _defaultGuideRefreshTimes = new[] { "06:30" };
        }

        private async Task RemoveAllSensorsOnStartup()
        {
            var sensors = GetSensors();
            var prefix = GetSensorPrefix();

            _logger.LogDebug($"{sensors.Count} entities found with EnityId that starts with 'sensor.{prefix}_' for cleanup");

            foreach (var sensor in sensors)
            {
                _logger.LogDebug($"Delete sensor: {sensor.EntityId}");
                await _entityManager.RemoveAsync(sensor.EntityId).ConfigureAwait(false);
            }
        }

        private async Task SetAvailabilityForAllSensorsToDown()
        {
            var sensors = GetSensors();

            foreach(var sensor in sensors)
            {
                await _entityManager.SetAvailabilityAsync(sensor.EntityId, "down").ConfigureAwait(false);
            }
        }

        private List<Entity> GetSensors()
        {
            var allEntities = _haContext.GetAllEntities();

            var prefix = GetSensorPrefix();

            return allEntities.Where(i => i.EntityId.StartsWith($"sensor.{prefix}_")).ToList();
        }

        private string GetSensorPrefix()
        {
            var prefix = "epg";

            if (!string.IsNullOrEmpty(_config.Value.SensorPrefix))
            {
                prefix = _config.Value.SensorPrefix.ToSimple();
            }

            return prefix;
        }

        private void PrintDataProviderChannelList()
        {
            var outputList = $"Channel List for Hoerzu data provider{Environment.NewLine}";
            foreach(var channel in HoerzuStation.GetAll().Select(s => s.Name).OrderBy(o => o))
            {
                outputList += $"- \"{channel}\"{Environment.NewLine}";
            }

            _logger.LogInformation(outputList);
            
        }

        private void Exit()
        {
            _logger.LogInformation("Terminating application normaly.");
            _liveTime.StopApplication();
        }
        #endregion
    }
}