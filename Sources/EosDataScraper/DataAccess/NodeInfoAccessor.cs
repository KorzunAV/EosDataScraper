using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Npgsql;
using System.Threading;
using System.Threading.Tasks;
using EosDataScraper.Models;

namespace EosDataScraper.DataAccess
{
    public static class NodeInfoAccessor
    {
        public static async Task<List<NodeInfo>> GetAllNodeInfo(this NpgsqlConnection connection, CancellationToken token)
        {
            var cmd = @"SELECT id, url, success_count, fail_count, elapsed_milliseconds FROM public.node_info;";

            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            var nInfos = new List<NodeInfo>();
            using (var reader = await command.ExecuteReaderAsync(token))
            {
                while (reader.Read())
                {
                    var ni = new NodeInfo
                    {
                        Id = reader.GetIntegerOrDefault(0),
                        Url = reader.GetStringOrDefault(1),
                        SuccessCount = reader.GetIntegerOrDefault(2),
                        FailCount = reader.GetIntegerOrDefault(3),
                        ElapsedMilliseconds = reader.GetIntegerOrDefault(4)
                    };
                    nInfos.Add(ni);
                }
            }
            return nInfos;
        }

        public static Task UpdateAsync(this NpgsqlConnection connection, NodeInfo node, CancellationToken token)
        {
            var cmd = $@"UPDATE public.node_info
                         SET success_count={node.SuccessCount}, fail_count={node.FailCount}, elapsed_milliseconds={node.ElapsedMilliseconds}
                         WHERE id= {node.Id};";

            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            return command.ExecuteNonQueryAsync(token);
        }

        public static Task InsertNodeInfos(this NpgsqlConnection connection, List<NodeInfo> nodes, CancellationToken token)
        {
            if (!nodes.Any())
                throw new NullReferenceException(nameof(nodes));

            var sb = new StringBuilder();
            sb.AppendLine("INSERT INTO public.node_info(url, success_count, fail_count, elapsed_milliseconds) VALUES");
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                sb.AppendLine($"('{node.Url}', {node.SuccessCount}, {node.FailCount}, {node.ElapsedMilliseconds}){(i < nodes.Count - 1 ? "," : ";")}");
            }

            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = sb.ToString()
            };
            return command.ExecuteNonQueryAsync(token);
        }

        public static Task DeleteNodeInfos(this NpgsqlConnection connection, List<NodeInfo> nodes, CancellationToken token)
        {
            if (!nodes.Any())
                throw new NullReferenceException(nameof(nodes));

            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = $"DELETE FROM public.node_info WHERE url IN ('{string.Join("','", nodes.Select(n => n.Url))}')"
            };
            return command.ExecuteNonQueryAsync(token);
        }
    }
}
