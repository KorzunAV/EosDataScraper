using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EosDataScraper.Common;
using EosDataScraper.Extensions;
using EosDataScraper.Models;
using Npgsql;

namespace EosDataScraper.DataAccess
{
    public static class ActionAccessor
    {
        public static Task AddPrimaryKeyAndRelations(this NpgsqlConnection connection, CancellationToken token)
        {
            var cmd = @"CREATE INDEX IF NOT EXISTS token_transaction_id_pk ON public.token USING hash (transaction_id);
                        CREATE INDEX IF NOT EXISTS delayed_transaction_transaction_id_pk ON public.delayed_transaction USING hash (transaction_id);";
            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            return command.ExecuteNonQueryAsync(token);
        }

        public static async Task AddPartitionsPrimaryKeyAndRelations(this NpgsqlConnection connection, CancellationToken token)
        {
            string table = "transfer";
            var cmd = $@"SELECT relname FROM pg_class WHERE relname like '{table}_%';";
            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            var result = new HashSet<string>();

            using (var reader = await command.ExecuteReaderAsync(token))
            {
                while (reader.Read())
                {
                    result.Add(reader.GetStringOrDefault(0));
                }
            }

            if (!result.Any())
                return;

            var pNameRegexp = new Regex($@"^{table}_[\d]{{4}}_[\d]{{2}}_[\d]{{2}}$");

            foreach (var itm in result)
            {
                if (pNameRegexp.IsMatch(itm))
                {
                    await CreateIndex(connection, itm, "to", result, token);
                    await CreateIndex(connection, itm, "from", result, token);
                    await CreateIndex(connection, itm, "action_contract", result, token);
                }
            }
        }

        private static async Task CreateIndex(NpgsqlConnection connection, string table, string column, HashSet<string> set, CancellationToken token)
        {
            if (set.Contains($"{table}_{column}_idx"))
                return;

            var cmd = $@"CREATE INDEX IF NOT EXISTS {table}_{column}_idx ON public.{table} USING btree (""{column}""); ";
            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            await command.ExecuteNonQueryAsync(token);
        }

        public static Task UpdateTokenCost(this NpgsqlConnection connection, List<TokenCost> tokenCosts, CancellationToken token)
        {
            var sb = new StringBuilder();
            var command = new NpgsqlCommand
            {
                Connection = connection
            };

            for (var i = 0; i < tokenCosts.Count; i++)
            {
                var itm = tokenCosts[i];
                sb.AppendLine($"UPDATE public.token SET eos_rate = @p{i}_1, usd_rate = @p{i}_2 WHERE action_contract = @p{i}_3 AND token_name = @p{i}_4;");
                command.Parameters.AddValue($"@p{i}_1", itm.EosRate);
                command.Parameters.AddValue($"@p{i}_2", itm.UsdRate);
                command.Parameters.AddValue($"@p{i}_3", itm.Contract);
                command.Parameters.AddValue($"@p{i}_4", itm.TokenName);
            }

            command.CommandText = sb.ToString();

            return command.ExecuteNonQueryAsync(token);
        }

        public static Task UpdateDelayedTokenAsync(this NpgsqlConnection connection, CancellationToken token)
        {
            var cmd = @"UPDATE public.token
                        SET 
                           transaction_status=delayed_transaction.transaction_status, 
                           close_block_num=delayed_transaction.block_num
                        FROM public.delayed_transaction
                        WHERE token.transaction_id = delayed_transaction.transaction_id 
                          AND token.block_num <= delayed_transaction.block_num 
                          AND token.transaction_status != delayed_transaction.transaction_status;";

            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            return command.ExecuteNonQueryAsync(token);
        }

        public static Task CleanDelayedTransferAsync(this NpgsqlConnection connection, CancellationToken token)
        {
            //transactions were already processed or referenced to untrack operations
            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = "DELETE FROM public.delayed_transaction;"
            };

            return command.ExecuteNonQueryAsync(token);
        }

        public static async Task UpdateDelayedTransferAsync(this NpgsqlConnection connection, CancellationToken token)
        {
            var limit = 10000;
            var delSet = new List<TransferAction>();
            var container = new ScraperCashContainer();

            var get = $@"SELECT t.block_num, t.transaction_id, t.action_num, t.action_contract, t.action_name, 
                                t.transaction_expiration, t.from, t.to, t.quantity, t.token_name, t.memo_utf8, 
                                dt.transaction_status, dt.block_num AS close_block_num, dt.timestamp
                         FROM public.transfer_1970_01_01 t
                         JOIN public.delayed_transaction dt ON dt.transaction_id = t.transaction_id
                         WHERE t.block_num <= dt.block_num
                         ORDER BY dt.timestamp
                         LIMIT {limit};";
            do
            {
                NpgsqlTransaction transaction = null;
                try
                {
                    delSet.Clear();
                    container.Clear();

                    var command = new NpgsqlCommand
                    {
                        Connection = connection,
                        CommandText = get
                    };

                    using (var reader = await command.ExecuteReaderAsync(token))
                    {
                        while (reader.Read())
                        {
                            var itm = new TransferAction
                            {
                                BlockNum = reader.GetLongOrDefault(0),
                                TransactionId = reader.GetBytesOrDefault(1),
                                ActionNum = reader.GetIntegerOrDefault(2),
                                ActionContractId = reader.GetULongOrDefault(3),
                                ActionName = reader.GetStringOrDefault(4),
                                TransactionExpiration = reader.GetDateTimeOrDefault(5),
                                FromId = reader.GetULongOrDefault(6),
                                ToId = reader.GetULongOrDefault(7),
                                Quantity = reader.GetDecimalOrDefault(8),
                                TokenName = reader.GetStringOrDefault(9),
                                MemoUtf8 = reader.GetBytesOrDefault(10),
                                TransactionStatus = reader.GetNullableStatusEnum(11),
                                CloseBlockNum = reader.GetLongOrNull(12),
                                Timestamp = reader.GetDateTimeOrDefault(13),
                            };

                            container.AddToBuffer(itm);
                            delSet.Add(itm);
                        }
                    }

                    if (!delSet.Any())
                        return;

                    transaction = connection.BeginTransaction();

                    var dComm = new NpgsqlCommand
                    {
                        Connection = connection
                    };

                    var sb = new StringBuilder();
                    for (var i = 0; i < delSet.Count; i++)
                    {
                        var itm = delSet[i];
                        sb.AppendLine($"DELETE FROM public.transfer_1970_01_01 WHERE block_num = @p1_{i} AND transaction_id = @p2_{i} AND action_num = @p3_{i};");
                        dComm.Parameters.AddValue($"p1_{i}", itm.BlockNum);
                        dComm.Parameters.AddValue($"p2_{i}", itm.TransactionId);
                        dComm.Parameters.AddValue($"p3_{i}", itm.ActionNum);
                    }

                    dComm.CommandText = sb.ToString();
                    await dComm.ExecuteNonQueryAsync(token);

                    container.FlushBuffer();

                    await container.CommitAndDispose(connection, long.MaxValue, token);

                    transaction.CommitAndDispose();
                }
                catch
                {
                    transaction.RollbackAndDispose();
                    throw;
                }
            } while (delSet.Count == limit);
        }

        public static async Task<bool> IsTransferPartitionExist(this NpgsqlConnection connection, DateTime day, CancellationToken token)
        {
            var cmd = $"SELECT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename = 'transfer_{day:yyyy_MM_dd}');";

            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            var obj = await command.ExecuteScalarAsync(token);
            return obj != null && (bool)obj;
        }

        public static async Task CreateTransferPartitionIfNotExist(this NpgsqlConnection connection, DateTime day, CancellationToken token)
        {
            var isExists = await IsTransferPartitionExist(connection, day, token);
            if (isExists)
                return;

            var cmd = $@"CREATE TABLE transfer_{day:yyyy_MM_dd} PARTITION OF transfer FOR VALUES FROM ('{day:yyyy-MM-dd}') TO ('{day.AddDays(1):yyyy-MM-dd}');";

            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            await command.ExecuteNonQueryAsync(token);
        }

        public static async Task<string> GetTransfersInfoLastState(this NpgsqlConnection connection, CancellationToken token)
        {
            var cmd = @"SELECT json FROM public.service_state WHERE service_id = 2;";

            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };


            var result = await command.ExecuteScalarAsync(token);
            if (result == DBNull.Value)
                return string.Empty;
            return (string)result;
        }

        public static async Task UpdateTransferInfo(this NpgsqlConnection connection, CancellationToken token)
        {
            var currentState = await GetTransfersInfoLastState(connection, token);
            var cmd = $@"SELECT relname
                         FROM pg_class 
                         WHERE relname SIMILAR TO 'transfer_[\d]{4}_[\d]{2}_[\d]{2}' 
{(string.IsNullOrEmpty(currentState) ? string.Empty : "AND relname > (SELECT json FROM public.service_state WHERE service_id = 2 )")}
                         ORDER BY relname;";


            var command = new NpgsqlCommand
            {
                Connection = connection,
                CommandText = cmd
            };

            var tables = new List<string>();

            using (var reader = await command.ExecuteReaderAsync(token))
            {
                while (reader.Read())
                {
                    tables.Add(reader.GetStringOrDefault(0));
                }
            }

            if (tables.Count < 2)
                return;

            foreach (var table in tables.Take(tables.Count - 1))
            {
                NpgsqlTransaction transaction = null;
                try
                {
                    var dts = table.Substring(table.Length - 10);
                    var dt = DateTime.ParseExact(dts, "yyyy_MM_dd", CultureInfo.InvariantCulture);

                    //TODO: KOA (SELECT DISTINCT contract FROM public.dapp_contract) - it`s a crutch dapp_contract.contract must be uniq
                    var upd =
                        $@"
                           DELETE FROM transfers_info WHERE ""timestamp"" = '{dt:yyyy-MM-dd}';
                           INSERT INTO public.transfers_info(""timestamp"", action_contract, token_name, contract, ""sum"", ""count"", ""from_count"", ""to_count"", ""type"")
                           SELECT t.timestamp, t.action_contract, t.token_name, t.from AS contract, SUM(t.quantity) AS ""sum"", COUNT(*) AS ""count"", COUNT(DISTINCT t.from) AS ""from_count"", COUNT(DISTINCT t.to) AS ""to_count"", 0 AS ""type""
                           FROM {table} t
                           JOIN (SELECT DISTINCT contract FROM public.dapp_contract) dc ON dc.contract = t.from
                           WHERE t.transaction_status = 0
                           GROUP BY t.timestamp, t.action_contract, t.token_name, t.from
                           UNION
                           SELECT t.timestamp, t.action_contract, t.token_name, t.to AS contract, SUM(t.quantity) AS ""sum"", COUNT(*) AS ""count"", COUNT(DISTINCT t.from) AS ""from_count"", COUNT(DISTINCT t.to) AS ""to_count"", 1 AS ""type""
                           FROM {table} t
                           JOIN (SELECT DISTINCT contract FROM public.dapp_contract) dc ON dc.contract = t.to
                           WHERE t.transaction_status = 0
                           GROUP BY t.timestamp, t.action_contract, t.token_name, t.to
                           ON CONFLICT (""timestamp"", action_contract, token_name, contract, ""type"") DO NOTHING;
                            
                           UPDATE service_state SET json='{table}' WHERE service_id = 2 AND json = '{currentState}';
                           SELECT json FROM service_state WHERE service_id = 2 ";

                    transaction = connection.BeginTransaction();

                    var cmdUpdate = new NpgsqlCommand
                    {
                        Connection = connection,
                        CommandText = upd
                    };

                    var newTable = string.Empty;
                    using (var reader = await cmdUpdate.ExecuteReaderAsync(token))
                    {
                        while (reader.Read())
                        {
                            newTable = reader.GetStringOrDefault(0);
                        }
                    }

                    if (table.Equals(newTable))
                    {
                        transaction.CommitAndDispose();
                        currentState = newTable;
                    }
                    else
                    {
                        transaction.RollbackAndDispose();
                        return;
                    }
                }
                catch
                {
                    transaction.RollbackAndDispose();
                    throw;
                }
            }
        }
    }
}