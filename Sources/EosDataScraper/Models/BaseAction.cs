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
    public abstract class BaseAction : BaseTable
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
        
        [Column("action_num")]
        public int ActionNum { get; set; }
        

        #region Account

        [NotMapped]
        [JsonProperty("action_contract", NullValueHandling = NullValueHandling.Ignore)]
        public string ActionContract
        {
            get => ActionContractId > 0 ? BaseName.UlongToString(ActionContractId) : null;
            set => ActionContractId = BaseName.StringToName(value);
        }

        [Column("action_contract")]
        public ulong ActionContractId { get; set; }

        #endregion

        [JsonProperty("action_name", NullValueHandling = NullValueHandling.Ignore)]
        [Column("action_name")]
        public string ActionName { get; set; }

        [JsonProperty("transaction_expiration", NullValueHandling = NullValueHandling.Ignore)]
        [Column("transaction_expiration")]
        public DateTime TransactionExpiration { get; set; }

        [JsonProperty("transaction_status", NullValueHandling = NullValueHandling.Ignore)]
        [Column("transaction_status")]
        public StatusEnum? TransactionStatus { get; set; }

        [JsonProperty("close_block_num", NullValueHandling = NullValueHandling.Ignore)]
        [Column("close_block_num")]
        public long? CloseBlockNum { get; set; }


        public override void AppendTableName(StringBuilder sb)
        {
            sb.Append("public.action");
        }

        public override void AppendColNames(StringBuilder sb)
        {
            sb.Append("action_num, action_contract, action_name, block_num, transaction_id, transaction_expiration, transaction_status, close_block_num");
        }

        public override void AppendColValNames(StringBuilder sb)
        {
            sb.Append("@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8");
        }

        public override void Import(NpgsqlBinaryImporter writer)
        {
            writer.StartRow();
            writer.WriteValue(ActionNum);
            writer.WriteValue(ActionContractId);
            writer.WriteValue(ActionName);
            writer.WriteValue(BlockNum);
            writer.WriteValue(TransactionId);
            writer.WriteValue(TransactionExpiration);
            writer.WriteValue(TransactionStatus);
            writer.WriteValue(CloseBlockNum);
        }

        public override string InsertCommandText()
        {
            var sb = new StringBuilder("INSERT INTO ");
            AppendTableName(sb);
            sb.Append("(");
            AppendColNames(sb);
            sb.Append(") VALUES (");
            AppendColValNames(sb);
            sb.Append(") ON CONFLICT DO NOTHING;");

            return sb.ToString();
        }
    }
}