using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EPG.Models
{
    internal class DataProvider
    {
        public string? Fullname { get; set; }
        public IEnumerable<string>? Stations { get; set; }
        public IEnumerable<string>? RefreshTimes { get; set; }
    }
}