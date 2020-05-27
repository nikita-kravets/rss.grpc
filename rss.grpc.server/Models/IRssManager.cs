using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace rss.grpc.server.Models
{
    public interface IRssManager
    {
        Task<int> AddClient(string name, string email);
        Task<bool> Subscribe(int id, string tag);
        Task<bool> Unsubscribe(int id, string tag);
        Task<Client> GetClient(int id);
        Task<bool> HasRss(string guid);
        Task<int> CreateRss(string guid, string xml, DateTime date);
        Task<bool> RssAddTag(int rssId, string tag);
        Task<List<Rss>> RssForClient(int clientId, int startId, DateTime minDate);
    }
}
