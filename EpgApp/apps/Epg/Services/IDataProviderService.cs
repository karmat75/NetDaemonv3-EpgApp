using EpgApp.apps.Epg.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EpgApp.apps.Epg.Services
{
    public interface IDataProviderService
    {
        string ProviderName { get; }
        Task<IEnumerable<Show>> LoadShowsAsync(string station);
        Task<string> GetDescriptionAsMarkdown(Show show);
        string GetLink(Show show);
    }
}