using EpgApp.apps.Epg.Models;
using EpgApp.apps.Epg.Services;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EpgApp.apps.Epg
{
    public class StationGuide
    {
        private readonly ILogger<Epg> _logger;
        private readonly string _station;
        private readonly IDataProviderService _dataProviderService;
        private readonly IMqttEntityManager _entityManager;
        private readonly IScheduler _scheduler;
        private readonly IEnumerable<string> _guideRefreshTimes;
        private readonly int _refreshrateInSeconds;
        private readonly IHaContext _haContext;
        private readonly IAppConfig<Config> _config;

        private readonly Regex _timeRegex = new Regex(@"^([0-1]?[0-9]|2[0-3]):([0-5][0-9])(?::([0-5][0-9]))?$");  // 09:30 / 9:30 / 09:30:00 / 9:30:00
        private IEnumerable<Show> _guide;
        private Show? _lastShow;
        private static int _instanceCounter = 0;
        private readonly int _instanceNumber;

        public StationGuide(
            StationGuideDTO stationGuideArguments)
        {
            _logger = stationGuideArguments.Logger;
            _dataProviderService = stationGuideArguments.DataProviderService;
            _station = stationGuideArguments.Station;
            _scheduler = stationGuideArguments.Scheduler;
            _guideRefreshTimes = stationGuideArguments.GuideRefreshTimes;
            _refreshrateInSeconds = stationGuideArguments.RefreshrateInSeconds;
            _haContext = stationGuideArguments.HomeAssistantContext;
            _entityManager = stationGuideArguments.MqttEntityManager;
            _config = stationGuideArguments.Config;

            _instanceNumber = _instanceCounter++;
        }

        public async Task InitializeAsync()
        {
            await RefreshGuideAsync();

            foreach (var refreshTime in _guideRefreshTimes)
            {
                var time = FormatTime(refreshTime)?.Split(':');
                if (time == null)
                {
                    continue;
                }
                _= _scheduler.ScheduleCron($"{time[1]} {time[0]} * * *", async () => await RefreshGuideAsync());
            }

            //_= _scheduler.Schedule(TimeSpan.FromSeconds(_refreshrateInSeconds), async () => await GetCurrentShowAndSetSensorAsync());
        }

        public async Task RefreshGuideAsync()
        {
            InitializeGuideEnumerable();

            try
            {
                _guide = await _dataProviderService.LoadShowsAsync(_station);
                if(_lastShow != null)
                {
                    _lastShow = null;
                }

                await ClearSensor(GetSensorName(_station));
                await GetCurrentShowAndSetSensorAsync();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $@"Error when trying to refresh '{_station}' [EPG: {_dataProviderService.ProviderName}]:\r\n{ex.Message}.");
            }
        }

        public Show? GetCurrentShow(DateTime now)
        {
            return _guide.Where(s => s.Start <= now).OrderByDescending(s => s.Start).FirstOrDefault();
        }

        public Show? GetUpcomingShow(DateTime now)
        {
            return _guide.Where(s => s.Start > now).OrderBy(s => s.Start).FirstOrDefault();
        }

        public async Task<string> GetDescription(Show show)
        {
            return await _dataProviderService.GetDescriptionAsMarkdown(show);
        }

        public string GetLink(Show show)
        {
            return _dataProviderService.GetLink(show);
        }

        private async Task GetCurrentShowAndSetSensorAsync()
        {
            var now = DateTime.Now;
            var currentShow = GetCurrentShow(now);
            var upcomingShow = GetUpcomingShow(now);

            var sensorName = GetSensorName(_station);
            await CreateSensorIfNeededAsync(sensorName);

            // Clear Sensor, if no current show found
            if (currentShow == null)
            {
                _logger.LogError($@"Cannot find current TV show for station ""{_station}"" [EPG: {_dataProviderService.ProviderName}]");
                await ClearSensor(sensorName);
                return;
            }

            var state = GetState(sensorName);

            if (_lastShow == null || !_lastShow.Title.Equals(currentShow.Title, StringComparison.Ordinal) || !_lastShow.Start.Equals(currentShow.Start))
            {

                var sensorAttributes = new ShowAttributes
                {
                    Station = _station,
                    Title = currentShow.Title,
                    Episode = currentShow.Episode,
                    BeginTime = currentShow.Start.ToShortTimeString(),
                    Duration = currentShow.DurationInMinutes ?? 0,
                    Genre = currentShow.Category,
                    Upcoming = upcomingShow?.Title ?? string.Empty,
                    DataProvider = _dataProviderService.ProviderName,
                    Description = await GetDescription(currentShow),
                    Link = GetLink(currentShow),
                };

                await _entityManager.SetAttributesAsync(sensorName, sensorAttributes).ConfigureAwait(false);

                state = GetState(sensorName);

                _logger.LogInformation($@"{_dataProviderService.ProviderName} / {_station}: TV Show ""{state?.State}"" started at {currentShow.Start.ToShortTimeString()}");

                try
                {
                    if (sensorAttributes.BeginTime != null)
                    {
                        _lastShow = currentShow;  // setting state and attribute went ok
                        _logger.LogDebug($@"{_dataProviderService.ProviderName} / {_station}: TV Show ""{state?.State}"" setting properties was successful");
                    }
                    else
                    {
                        _lastShow = null; // make sure, sensor will be set at next run again.
                        _logger.LogDebug($@"{_dataProviderService.ProviderName} / {_station}: TV Show ""{state?.State}"" need to set properties again.");
                    }
                }
                catch
                {
                    _lastShow = null;  // make sure, sensor will be set at next run again.
                    _logger.LogDebug($@"{_dataProviderService.ProviderName} / {_station}: TV Show ""{state?.State}"" need to set properties again.");
                }
            }

            state = GetState(sensorName);
            if(state != null)
            {
                await _entityManager.SetAvailabilityAsync(sensorName, "up").ConfigureAwait(false);
                int currentDuration = 0;
                if(currentShow.DurationInPercent > 0)
                {
                    currentDuration = (int)((currentShow.DurationInPercent ?? 0.0) * 100);
                }

                await _entityManager.SetStateAsync(sensorName, $"{currentDuration}").ConfigureAwait(false);
            }
            

            _logger.LogDebug($@"{_dataProviderService.ProviderName} / {_station}: ""{currentShow.Title}"" running since {currentShow?.Start.ToShortTimeString()} {currentShow?.Title ?? "-"} ({currentShow?.DurationInPercent:P1})");
            _ = _scheduler.Schedule(TimeSpan.FromSeconds(_refreshrateInSeconds), async () => await GetCurrentShowAndSetSensorAsync());

        }

        private async Task CreateSensorIfNeededAsync(string sensorName)
        {
            var sensor = _haContext.Entity(sensorName);

            if (sensor.EntityState == null)
            {
                var entityCreateOptions = new EntityCreationOptions
                {
                    Persist = false,
                    PayloadAvailable = "up",
                    PayloadNotAvailable = "down",                    
                };

                var additonalConfig = new
                {
                    unit_of_measurement = "%"
                };

                await _entityManager.CreateAsync(sensorName, entityCreateOptions, additonalConfig).ConfigureAwait(false);
            }
        }

        private string GetSensorName(string station)
        {
            var prefix = "epg";

            if (!string.IsNullOrEmpty(_config.Value.SensorPrefix))
            {
                prefix = _config.Value.SensorPrefix.ToSimple();
            }
            return $"sensor.{prefix}_{_dataProviderService.ProviderName.ToSimple()}_{station.ToSimple()}";
        }

        private EntityState? GetState(string sensorname)
        {
            return _haContext.GetState(sensorname);
        }

        private async Task ClearSensor(string sensorName)
        {
            await _entityManager.RemoveAsync(sensorName);
            _logger.LogInformation($"Sensor {sensorName} removed successfull.");
        }

        private string? FormatTime(string time)
        {
            var match = _timeRegex.Match(time);
            if (!match.Success)
            {
                _logger.LogError($"'{time}' is not a valid Time.");
                return default;
            }

            if (time[1] == ':') time = "0" + time; // adding leading zero 

            if (time.Length == 5)
            {
                var seconds = _instanceNumber * 2 % 60;
                time += $":{seconds:00}";  // adding seconds
            }
            return time;
        }

        private void InitializeGuideEnumerable()
        {
            _guide = Enumerable.Empty<Show>();
        }
    }
}
