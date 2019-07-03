using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Newtonsoft.Json;
using Npgsql;

namespace EosDataScraper.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class BaseTable : IComparable<BaseTable>
    {
        [JsonProperty("block_num", NullValueHandling = NullValueHandling.Ignore)]
        [Column("block_num")]
        public long BlockNum { get; set; }



        public abstract void AppendTableName(StringBuilder sb);

        public abstract void AppendColNames(StringBuilder sb);

        public abstract void AppendColValNames(StringBuilder sb);

        public abstract void Import(NpgsqlBinaryImporter writer);

        public string CopyCommandText()
        {
            var sb = new StringBuilder("COPY ");
            AppendTableName(sb);
            sb.Append("(");
            AppendColNames(sb);
            sb.Append(") FROM STDIN (FORMAT BINARY)");
            return sb.ToString();
        }

        public virtual string InsertCommandText()
        {
            var sb = new StringBuilder("INSERT INTO ");
            AppendTableName(sb);
            sb.Append("(");
            AppendColNames(sb);
            sb.Append(") VALUES (");
            AppendColValNames(sb);
            sb.Append(");");

            return sb.ToString();
        }

        public int CompareTo(BaseTable other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return BlockNum.CompareTo(other.BlockNum);
        }
    }
}
