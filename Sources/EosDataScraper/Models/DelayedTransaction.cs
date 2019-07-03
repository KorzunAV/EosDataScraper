using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Cryptography.ECDSA;
using Ditch.EOS.Models;
using EosDataScraper.DataAccess;
using Newtonsoft.Json;
using Npgsql;

namespace EosDataScraper.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    [Table("delayed_transaction")]
    public class DelayedTransaction : BaseTable
    {
        #region TransactionId

        [Column("transaction_id")]
        public byte[] TransactionId { get; set; }


        [JsonProperty("transaction_id", NullValueHandling = NullValueHandling.Ignore)]
        public string TransactionIdHex
        {
            get => Hex.ToString(TransactionId);
            set => TransactionId = Hex.HexToBytes(value);
        }

        #endregion TransactionId

        [JsonProperty("transaction_status", NullValueHandling = NullValueHandling.Ignore)]
        [Column("transaction_status")]
        public StatusEnum TransactionStatus { get; set; }

        
        [JsonProperty("timestamp")]
        [Column("timestamp")]
        public DateTime Timestamp { get; set; }


        public override void AppendTableName(StringBuilder sb)
        {
            sb.Append("public.delayed_transaction");
        }

        public override void AppendColNames(StringBuilder sb)
        {
            sb.Append("block_num, transaction_id, transaction_status, timestamp");

        }

        public override void AppendColValNames(StringBuilder sb)
        {
            sb.Append("@p1, @p2, @p3, @p4");
        }

        public override void Import(NpgsqlBinaryImporter writer)
        {
            writer.StartRow();
            writer.WriteValue(BlockNum);
            writer.WriteValue(TransactionId);
            writer.WriteValue(TransactionStatus);
            writer.WriteValue(Timestamp);
        }
    }
}
