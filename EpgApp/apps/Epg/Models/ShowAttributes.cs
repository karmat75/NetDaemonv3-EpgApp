using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EpgApp.apps.Epg.Models
{
    public class ShowAttributes
    {
        public string? Station { get; set; }
        public string? Title { get; set; }
        public string? Episode { get; set; }
        public string? BeginTime { get; set; }
        public int Duration { get; set; }
        public string? Genre { get; set; }
        public string? Upcoming { get; set; }
        public string? DataProvider { get; set; }
        public string? Description { get; set; }
    }
}
