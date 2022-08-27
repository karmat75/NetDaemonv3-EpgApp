using EpgApp.apps.Epg.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EpgApp.apps.Epg.Services
{
    public interface IDataProviderService
    {
        string ProviderName { get; }
        Task<IEnumerable<Show>> LoadShowsAsync(string station);
        Task<string> GetDescriptionAsMarkdown(Show show);
    }
}