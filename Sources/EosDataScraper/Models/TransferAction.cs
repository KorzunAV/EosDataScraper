using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;
using System.Text;
using Ditch.EOS.Models;
using EosDataScraper.Common;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;

namespace EosDataScraper.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    [Table("transfer")]
    public class TransferAction : BaseAction
    {
        public const string ActionKey = "transfer";

        #region From

        [NotMapped]
        [JsonProperty("from", NullValueHandling = NullValueHandling.Ignore)]
        public string From
        {
            get => FromId > 0 ? BaseName.UlongToString(FromId) : null;
            set => FromId = BaseName.StringToName(value);
        }

        [Required]
        [Column("from")]
        public ulong FromId { get; set; }

        #endregion

        #region To

        [NotMapped]
        [JsonProperty("to", NullValueHandling = NullValueHandling.Ignore)]
        public string To
        {
            get => ToId > 0 ? BaseName.UlongToString(ToId) : null;
            set => ToId = BaseName.StringToName(value);
        }

        [Required]
        [Column("to")]
        public ulong ToId { get; set; }

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

        [NotMapped]
        [JsonProperty("memo", NullValueHandling = NullValueHandling.Ignore)]
        public string Memo
        {
            get => MemoUtf8 != null && MemoUtf8.Any() ? Encoding.Default.GetString(MemoUtf8) : string.Empty;
            set => MemoUtf8 = Encoding.Default.GetBytes(value);
        }

        [Column("memo_utf8")]
        public byte[] MemoUtf8 { get; set; }

        [JsonProperty("timestamp")]
        [Column("timestamp")]
        public DateTime Timestamp { get; set; } = AdoExtension.DefaultDateTime;


        public override void AppendTableName(StringBuilder sb)
        {
            sb.Append($"public.transfer_{Timestamp:yyyy_MM_dd}");
        }

        public override void AppendColNames(StringBuilder sb)
        {
            base.AppendColNames(sb);

            sb.Append(", \"from\", \"to\", quantity, token_name, memo_utf8, timestamp");
        }

        public override void AppendColValNames(StringBuilder sb)
        {
            base.AppendColValNames(sb);

            sb.Append(", @p2_1, @p2_2, @p2_3, @p2_4, @p2_5, @p2_6");
        }

        public override void Import(NpgsqlBinaryImporter writer)
        {
            base.Import(writer);

            writer.WriteValue(FromId);
            writer.WriteValue(ToId);
            writer.WriteValue(Quantity);
            writer.WriteValue(TokenName);
            writer.WriteValue(MemoUtf8);
            writer.WriteValue(Timestamp, NpgsqlDbType.Date);
        }
    }
}
