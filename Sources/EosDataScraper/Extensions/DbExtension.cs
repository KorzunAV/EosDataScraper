using System;
using System.Data.Common;
using Ditch.EOS.Models;
using Npgsql;
using NpgsqlTypes;

namespace EosDataScraper.Extensions
{
    public static class DbExtension
    {
        public static StatusEnum? GetNullableStatusEnum(this DbDataReader reader, int col)
        {
            var value = reader.GetValue(col);
            if (value == DBNull.Value)
                return null;
            return (StatusEnum)(short)value;
        }

        public static void WriteValue(this NpgsqlBinaryImporter binaryImporter, StatusEnum? value)
        {
            if (value.HasValue)
                binaryImporter.Write((byte)value.Value, NpgsqlDbType.Smallint);
            else
                binaryImporter.WriteNull();
        }
    }
}
