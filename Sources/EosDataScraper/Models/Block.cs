using System;
using System.ComponentModel.DataAnnotations;
using Ditch.EOS.Models;
using Newtonsoft.Json;

namespace EosDataScraper.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Block
    {
        [JsonProperty("id")]
        public byte[] Id { get; set; }

        [JsonProperty("block_num", NullValueHandling = NullValueHandling.Ignore)]
        public long BlockNum { get; set; }

        #region producer
        
        [JsonProperty("producer")]
        public string Producer
        {
            get => BaseName.UlongToString(ProducerId);
            set => ProducerId = BaseName.StringToName(value);
        }

        [Required]
        public ulong ProducerId { get; set; }

        #endregion

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
