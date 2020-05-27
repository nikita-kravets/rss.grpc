using System.ComponentModel.DataAnnotations.Schema;


namespace rss.grpc.server.Models
{
    public class RssToTag
    {
        [ForeignKey("Rss")]
        public int RssFk { get; set; }
        [ForeignKey("Tag")]
        public int TagFk { get; set; }
        public Tag Tag { get; set; }
        public Rss Rss { get; set; }
    }
}
