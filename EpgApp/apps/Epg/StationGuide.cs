using EpgApp.apps.Epg.Models;
using EpgApp.apps.Epg.Services;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
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
                _scheduler.ScheduleCron($"{time[1]} {time[0]} * * *", async () => await RefreshGuideAsync());
            }

            _scheduler.Schedule(TimeSpan.FromSeconds(_refreshrateInSeconds), async () => await GetCurrentShowAndSetSensorAsync());
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

        private async Task GetCurrentShowAndSetSensorAsync()
        {
            var now = DateTime.Now;
            var currentShow = GetCurrentShow(now);
            var upcomingShow = GetUpcomingShow(now);

            var sensorName = GetSensorName(_station);
            await CreateSensorAsync(sensorName);

            // Clear Sensor, if no current show found
            if (currentShow == null)
            {
                _logger.LogError($@"Cannot find current TV show for station ""{_station}"" [EPG: {_dataProviderService.ProviderName}]");
                await ClearSensor(sensorName);
                return;
            }

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
                    Description = "loading...",
                };

                await _entityManager.SetAttributesAsync(sensorName, sensorAttributes).ConfigureAwait(false);

                var state = _haContext.GetState(sensorName);

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

                var description = await GetDescription(currentShow);
                if (state?.Attributes != null)
                {
                    sensorAttributes.Description = description;
                    await _entityManager.SetAttributesAsync(sensorName, sensorAttributes);
                    state = _haContext.GetState(sensorName);
                    _logger.LogInformation($@"{_dataProviderService.ProviderName} / {_station}: description for ""{state?.State}"" loaded");
                }
            }

            _logger.LogDebug($@"{_dataProviderService.ProviderName} / {_station}: ""{currentShow.Title}"" running since {currentShow?.Start.ToShortTimeString()} {currentShow?.Title ?? "-"} ({currentShow?.DurationInPercent:P1})");
        }

        private async Task CreateSensorAsync(string sensorName)
        {
            var entityCreateOptions = new EntityCreationOptions
            {
                Persist = false
            };

            await _entityManager.CreateAsync(sensorName, entityCreateOptions).ConfigureAwait(false);
        }

        private string GetSensorName(string station)
        {
            return $"sensor.epg_{_dataProviderService.ProviderName.ToSimple()}_{station.ToSimple()}";
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
