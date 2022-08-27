using EpgApp.apps.Epg.Services;
using NetDaemon.Extensions.MqttEntityManager;
using System.Collections.Generic;
using System.Reactive.Concurrency;

namespace EpgApp.apps.Epg.Models
{
    public class StationGuideDTO
    {
        public ILogger<Epg> Logger { get; set; }
        public IDataProviderService DataProviderService { get; set; }
        public string Station { get; set; }
        public IMqttEntityManager MqttEntityManager { get; set; }
        public IScheduler Scheduler { get; set; }
        public IEnumerable<string> GuideRefreshTimes { get; set; }
        public int RefreshrateInSeconds { get; set; }
        public IHaContext HomeAssistantContext { get; set; }
        public IAppConfig<Config> Config { get; set; }
    }
}
