using System.Collections.Generic;

namespace EPG.Models
{
    internal class Config
    {
        public int? RefreshrateInSeconds { get; set; }
        public IEnumerable<DataProvider>? Dataproviders { get; set; }
    }
}