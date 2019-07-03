using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Ditch.EOS.Models;
using Newtonsoft.Json;

namespace EosDataScraper.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    [Table("dapp_contract")]
    public class DappContract
    {
        [Key]
        [JsonProperty("dapp_id")]
        [Column("dapp_id")]
        public int DappId { get; set; }

        #region Contract

        [NotMapped]
        [JsonProperty("contract")]
        public string Contract
        {
            get => BaseName.UlongToString(ContractId);
            set => ContractId = BaseName.StringToName(value);
        }

        [Column("contract")]
        public ulong ContractId { get; set; }
        
        #endregion
    }
}
