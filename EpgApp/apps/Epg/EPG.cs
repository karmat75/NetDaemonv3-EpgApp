using EpgApp.apps.Epg.Models;
using EpgApp.apps.Epg.Services;
using NetDaemon.Extensions.MqttEntityManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
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

        private List<DataProvider> _dataProviders;
        private int _refreshrateInSeconds;
        private IEnumerable<string> _defaultGuideRefreshTimes;

        public Epg(
            ILogger<Epg> appLogger, 
            IAppConfig<Config> appConfig,
            IMqttEntityManager mqttEntityManager,
            IScheduler scheduler,
            IHaContext haContext)
        {
            _logger = appLogger;
            _config = appConfig;
            _entityManager = mqttEntityManager;
            _scheduler = scheduler;
            _haContext = haContext;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            InitialiseDataproviderList();
            SetRefreshrateInSeconds();
            InitialisedefaultGuideRefreshTimes();

            await RunApplicationAsync(cancellationToken);
        }



        public async ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
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
        #endregion
    }
}