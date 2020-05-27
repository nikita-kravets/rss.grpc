using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;


namespace rss.grpc.client.Properties
{
    partial class Settings 
    {
        public ITagRebinder TagRebinder { get; set; }


        public override void Save()
        {
            base.Save();
            
            if (TagRebinder != null) {
                TagRebinder.RebindTags();
            }
            
        }
    }
}
