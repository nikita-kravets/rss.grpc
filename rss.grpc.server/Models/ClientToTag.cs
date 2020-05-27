using System.ComponentModel.DataAnnotations.Schema;


namespace rss.grpc.server.Models
{
    public class ClientToTag
    {
        [ForeignKey("Client")]
        public int ClientFk { get; set; }
        [ForeignKey("Tag")]
        public int TagFk { get; set; }
        public Client Client { get; set; }
        public Tag Tag { get; set; }
    }
}
