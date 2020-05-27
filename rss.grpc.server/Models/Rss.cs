using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace rss.grpc.server.Models
{
    public class Rss
    {
        public int RssId { get; set; }
        public string Guid { get; set; }
        public string FeedXml { get; set; }
        public DateTime PubDate { get; set; }
        [ForeignKey("RssFk")]
        public List<RssToTag> RssToTags { get; set; }
    }
}
