using System.Collections.Generic;

namespace EpgApp.apps.Epg.Models
{
    public class Config
    {
        public int? RefreshrateInSeconds { get; set; }
        public IEnumerable<DataProvider>? Dataproviders { get; set; }
    }
}