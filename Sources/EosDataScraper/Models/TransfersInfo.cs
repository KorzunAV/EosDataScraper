using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using Ditch.EOS.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace EosDataScraper.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    [Table("transfers_info")]
    public class TransfersInfo
    {
        [JsonProperty("timestamp")]
        [Column("timestamp")]
        public DateTime Timestamp { get; set; }

        #region ActionContract

        [NotMapped]
        [JsonProperty("action_contract")]
        public string ActionContract
        {
            get => ActionContractId > 0 ? BaseName.UlongToString((ulong)ActionContractId) : null;
            set => ActionContractId = BaseName.StringToName(value);
        }

        [Column("action_contract")]
        public decimal ActionContractId { get; set; }

        #endregion

        [NotMapped]
        [JsonProperty("quantity")]
        public Asset QuantityAsset
        {
            get => new Asset($"{Quantity} {TokenName}");
            set
            {
                Quantity = decimal.Parse(value.ToDoubleString(), CultureInfo.InvariantCulture);
                TokenName = value.Currency();
            }
        }

        [Required]
        [Column("quantity")]
        public decimal Quantity { get; set; }

        [Required]
        [MaxLength(8)] // \eos\contracts\eosiolib\symbol.hpp
        [Column("token_name")]
        public string TokenName { get; set; }

        #region Contract

        [NotMapped]
        [JsonProperty("contract")]
        public string Contract
        {
            get => ContractId > 0 ? BaseName.UlongToString((ulong)ContractId) : null;
            set => ContractId = BaseName.StringToName(value);
        }

        [Column("contract")]
        public decimal ContractId { get; set; }

        #endregion

        [Column("count")]
        [JsonProperty("count")]
        public long Count { get; set; }


        [Column("type")]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("type")]
        public TransferType Type { get; set; }
    }

    public enum TransferType
    {
        // ReSharper disable once UnusedMember.Global
        From = 0,
        To = 1
    }
}
