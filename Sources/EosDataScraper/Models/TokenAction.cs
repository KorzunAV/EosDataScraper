using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Text;
using Ditch.EOS.Models;
using EosDataScraper.Common;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;


namespace EosDataScraper.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    [Table("token")]
    public class TokenAction : BaseAction
    {
        public const string ActionKey = "create";

        #region Issuer

        [NotMapped]
        [JsonProperty("issuer")]
        public string Issuer
        {
            get => BaseName.UlongToString(IssuerId);
            set => IssuerId = BaseName.StringToName(value);
        }

        [Column("issuer")]
        public ulong IssuerId { get; set; }

        #endregion

        [NotMapped]
        [JsonProperty("maximum_supply")]
        public Asset MaximumSupplyAsset
        {
            get => new Asset($"{MaximumSupply} {TokenName}");
            set
            {
                MaximumSupply = decimal.Parse(value.ToDoubleString(), CultureInfo.InvariantCulture);
                TokenName = value.Currency();
            }
        }

        [Required]
        [Column("maximum_supply")]
        public decimal MaximumSupply { get; set; }

        [Required]
        [MaxLength(8)] // \eos\contracts\eosiolib\symbol.hpp
        [Column("token_name")]
        public string TokenName { get; set; }

        [Column("usd_rate")]
        [JsonProperty("usd_rate")]
        public decimal UsdRate { get; set; }

        [Column("eos_rate")]
        [JsonProperty("eos_rate")]
        public decimal EosRate { get; set; }


        public override void AppendTableName(StringBuilder sb)
        {
            sb.Append("public.token");
        }

        public override void AppendColNames(StringBuilder sb)
        {
            base.AppendColNames(sb);
            sb.Append(", issuer, maximum_supply, token_name, usd_rate, eos_rate");
        }

        public override void AppendColValNames(StringBuilder sb)
        {
            base.AppendColValNames(sb);
            sb.Append(", @p2_1, @p2_2, @p2_3, @p2_4, @p2_5");
        }

        public override void Import(NpgsqlBinaryImporter writer)
        {
            base.Import(writer);

            writer.WriteValue(IssuerId);
            writer.WriteValue(MaximumSupply);
            writer.WriteValue(TokenName, NpgsqlDbType.Varchar);
            writer.WriteValue(UsdRate);
            writer.WriteValue(EosRate);
        }
    }
}
