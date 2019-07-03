using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EosDataScraper.Common;
using EosDataScraper.Models;
using Npgsql;

namespace EosDataScraper.DataAccess
{
    public static class DappAccessor
    {
        public static async Task FindAndInsertOrUpdateAsync(this NpgsqlConnection connection, int minId, int maxId, Dapp dapp, ulong[] contracts, CancellationToken token)
        {
            var ids = await connection.SelectDappIdByKeyAsync(dapp.Slug, token);
            var id = ids.FirstOrDefault(i => i > minId && i < maxId);
            if (id == 0)
            {
                id = ids.FirstOrDefault(i => i < minId);
                if (id == 0)
                {
                    id = await connection.SelectMaxDappIdInRangeAsync(minId + minId, maxId, token);
                    if (id == 0)
                        id = minId + minId;

                    id++;
                }
                else
                {
                    id += minId;
                }
            }

            dapp.Id = id;
            await connection.InsertOrUpdateAsync(dapp, contracts, token);
        }

        public static async Task InsertOrUpdateAsync(this NpgsqlConnection connection, Dapp dApp, ulong[] contracts, CancellationToken token)
        {
            var cmd = @"INSERT INTO public.dapp(id, author, slug, description, title, url, category) VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7)
                        ON CONFLICT ON CONSTRAINT pk_dapp DO UPDATE SET (author, slug, description, title, url, category) = (@p2, @p3, @p4, @p5, @p6, @p7);";

            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            command.Parameters.AddValue("@p1", dApp.Id);
            command.Parameters.AddValue("@p2", dApp.Author);
            command.Parameters.AddValue("@p3", dApp.Slug);
            command.Parameters.AddValue("@p4", dApp.Description);
            command.Parameters.AddValue("@p5", dApp.Title);
            command.Parameters.AddValue("@p6", dApp.Url);
            command.Parameters.AddValue("@p7", dApp.Category);

            await command.ExecuteNonQueryAsync(token);
            await connection.ReInsertDappContractAsync(dApp.Id, contracts, token);
        }

        public static Task<int> ReInsertDappContractAsync(this NpgsqlConnection connection, int dappId, ulong[] contracts, CancellationToken token)
        {
            var sb = new StringBuilder($"DELETE FROM public.dapp_contract WHERE dapp_id = {dappId};");
            sb.AppendLine("INSERT INTO public.dapp_contract(contract, dapp_id) VALUES");

            for (var i = 0; i < contracts.Length; i++)
            {
                var contract = contracts[i];
                sb.AppendLine($"({contract}, {dappId}){(i == contracts.Length - 1 ? ";" : ",")}");
            }

            sb.AppendLine("UPDATE service_state SET json='transfer_1970_01_01' WHERE service_id = 2;");

            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = sb.ToString()
            };

            return command.ExecuteNonQueryAsync(token);
        }

        public static async Task<int> SelectMaxDappIdInRangeAsync(this NpgsqlConnection connection, int minId, int maxId, CancellationToken token)
        {
            var cmd = $"SELECT MAX(id) from public.dapp WHERE id > {minId} AND id < {maxId};";
            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            var result = await command.ExecuteScalarAsync(token);
            if (result is DBNull || result == null)
                return 0;

            return (int)result;
        }
        
        public static async Task<List<int>> SelectDappIdByKeyAsync(this NpgsqlConnection connection, string slug, CancellationToken token)
        {
            var cmd = "SELECT id from public.dapp WHERE slug ilike @p1 ORDER BY id;";
            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            command.Parameters.AddValue("@p1", slug);

            var result = new List<int>();
            using (var reader = await command.ExecuteReaderAsync(token))
            {
                while (reader.Read())
                {
                    result.Add(reader.GetIntegerOrDefault(0));
                }
            }

            return result;
        }

        public static async Task<bool> IsDappRadarExistAsync(this NpgsqlConnection connection, CancellationToken token)
        {
            var cmd = "SELECT count(*) FROM public.dapp WHERE id < 1000000;";

            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            var result = await command.ExecuteScalarAsync(token);
            if (result is DBNull || result == null)
                return false;
            return (long)result > 0;
        }

        public static async Task<bool> IsDappComExistAsync(this NpgsqlConnection connection, CancellationToken token)
        {
            var cmd = "SELECT count(*) FROM public.dapp WHERE id > 1000000 AND id < 3000000;";

            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            var result = await command.ExecuteScalarAsync(token);
            if (result is DBNull || result == null)
                return false;
            return (long)result > 0;
        }
    }
}