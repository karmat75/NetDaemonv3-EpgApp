using System.Collections.Generic;

namespace EpgApp.apps.Epg.Models
{
    public class DataProvider
    {
        public string? Fullname { get; set; }
        public IEnumerable<string>? Stations { get; set; }
        public IEnumerable<string>? RefreshTimes { get; set; }
    }
}