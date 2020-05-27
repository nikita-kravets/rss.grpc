using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace rss.grpc.server.Models
{
    public class Client
    {
        public int ClientId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        [ForeignKey("ClientFk")]
        public List<ClientToTag> ClientToTags { get; set; }
    }
}
