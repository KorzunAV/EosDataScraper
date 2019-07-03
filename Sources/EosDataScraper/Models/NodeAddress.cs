using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EosDataScraper.Models
{
    public class NodeAddress
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("isNode")]
        public bool IsNode { get; set; }

        [JsonProperty("nodes")]
        public List<JObject> Nodes { get; set; }
    }
}
