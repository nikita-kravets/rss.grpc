using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace rss.grpc.server.Models
{
    public class Tag
    {
        public int TagId { get; set; }
        public string TagName { get; set; }
        [ForeignKey("TagFk")]
        public List<RssToTag> RssToTags { get; set; }
        [ForeignKey("TagFk")]
        public List<ClientToTag> ClientToTags { get; set; }
    }
}
