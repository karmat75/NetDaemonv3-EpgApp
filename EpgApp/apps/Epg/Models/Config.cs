using System.Collections.Generic;

namespace EpgApp.apps.Epg.Models
{
    public class Config
    {
        public int? RefreshrateInSeconds { get; set; }
        public IEnumerable<DataProvider>? Dataproviders { get; set; }
        public bool? CleanupSensorsOnStartup { get; set; }
        public bool? OnlyCleanupAndEnd { get; set; }
        public string? SensorPrefix { get; set; } = "epg";
        public bool? PrintChannelsAndExit { get; set; }
    }
}