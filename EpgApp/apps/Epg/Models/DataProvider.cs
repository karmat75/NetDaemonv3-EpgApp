using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EpgApp.apps.Epg.Models
{
    public class DataProvider
    {
        public string? Fullname { get; set; }
        public IEnumerable<string>? Stations { get; set; }
        public IEnumerable<string>? RefreshTimes { get; set; }
    }
}