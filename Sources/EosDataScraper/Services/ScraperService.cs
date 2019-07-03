using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cryptography.ECDSA;
using Ditch.EOS.Models;
using EosDataScraper.Common;
using EosDataScraper.Common.DataAccess;
using EosDataScraper.Common.Services;
using EosDataScraper.DataAccess;
using EosDataScraper.Extensions;
using EosDataScraper.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace EosDataScraper.Services
{
    public sealed class ScraperService : BaseDbService
    {
        private readonly TemporaryLogManager _temporaryLogManager;
        private readonly BlockMiningService _blockMiningService;
        private readonly DateTime _start;
        private readonly ScraperCashContainer _container = new ScraperCashContainer();
        private readonly List<long> _blockIds = new List<long>();
        private ScraperState _scraperState;
        public const int ServiceId = 1;


        public int BlockRange { get; set; } = 5000;

        public ScraperService(ILogger<ScraperService> logger, IConfiguration configuration)
        : base(logger, configuration)
        {
            _temporaryLogManager = new TemporaryLogManager();
            _blockMiningService = new BlockMiningService { Callback = AddResult };
            _start = DateTime.Now;
        }

        public long MoveToMaxConsistentBlockNum(long blockId)
        {
            _blockIds.Sort();
            int count = 0;
            foreach (var b in _blockIds)
            {
                if (blockId + 1 == b)
                {
                    blockId++;
                    count++;
                }
                else
                {
                    break;
                }
            }

            if (count > 0)
                _blockIds.RemoveRange(0, count);
            return blockId;
        }

        protected override async Task DoSomethingAsync(NpgsqlConnection connection, CancellationToken token)
        {
            uint lastBlockNum = 0;
            _blockIds.Clear();
            _container.Clear();
            bool isLastBlock;
            var st = DateTime.Now;

            try
            {
                var count = BlockRange;

                _scraperState = await connection.GetServiceStateAsync<ScraperState>(ServiceId, token);
                await _blockMiningService.InitCashAsync(connection, _scraperState.BlockId, count, token);

                await _blockMiningService.StartAsync(_scraperState.BlockId + 1, _scraperState.BlockId + count, token);
                isLastBlock = count > _blockIds.Count;

                _temporaryLogManager.Add(new ScraperServiceTemporaryLog(st, _scraperState.BlockId, _blockIds.Count));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _temporaryLogManager.Add(new ScraperServiceTemporaryLog(e, st, _scraperState.BlockId));
                throw new Exception($"Last block num: {lastBlockNum}", e);
            }
            finally
            {
                if (!token.IsCancellationRequested && _scraperState != null)
                {
                    await BulkSaveAsync(connection, _container, _scraperState, token);
                }
            }

            if (isLastBlock)
            {
                await connection.AddPrimaryKeyAndRelations(token);
                await connection.AddPartitionsPrimaryKeyAndRelations(token);
                await connection.UpdateDelayedTokenAsync(token);
                await connection.UpdateDelayedTransferAsync(token);
                await connection.UpdateTransferInfo(token);

                await connection.CleanDelayedTransferAsync(token);

                await Task.Delay(TimeSpan.FromMinutes(5), token);
            }
        }

        private void AddResult(GetBlockResults result)
        {
            lock (_container)
            {
                Parse(_container, result);
                _blockIds.Add(result.BlockNum);
            }
        }


        private void Parse(ScraperCashContainer container, GetBlockResults result)
        {
            if (result.Transactions.Any())
            {
                foreach (var t in result.Transactions)
                {
                    if (t.Trx is JObject jObj)
                    {
                        var id = jObj.Value<string>("id");
                        var trx = jObj.Value<JObject>("transaction");
                        var expiration = trx.Value<DateTime>("expiration");
                        var actions = trx.Value<JArray>("actions");

                        for (var i = 0; i < actions.Count; i++)
                        {
                            var action = actions[i];
                            var a = action.ToActionOrNull(result, id, t.Status, i, expiration);

                            if (a != null)
                                container.AddToBuffer(a);
                        }
                    }
                    else
                    {
                        var trx = t.Trx as string;
                        var ts = new DelayedTransaction
                        {
                            BlockNum = result.BlockNum,
                            TransactionId = Hex.HexToBytes(trx),
                            TransactionStatus = t.Status,
                            Timestamp = result.Timestamp.Value
                        };
                        container.AddToBuffer(ts);
                    }
                }

                container.FlushBuffer();
            }
        }

        private async Task BulkSaveAsync(NpgsqlConnection connection, ScraperCashContainer container, ScraperState scraperState, CancellationToken token)
        {
            NpgsqlTransaction transaction = null;
            try
            {
                var st = DateTime.Now;
                transaction = connection.BeginTransaction();
                scraperState.BlockId = MoveToMaxConsistentBlockNum(scraperState.BlockId);
                var count = container.Count;
                await container.CommitAndDispose(connection, scraperState.BlockId, token);
                await connection.UpdateServiceStateAsync(ServiceId, JsonConvert.SerializeObject(scraperState), token);
                await _blockMiningService.UpdateNodeInfoAsync(connection, token);
                transaction.Commit();
                _temporaryLogManager.Add(new BulkSaveTemporaryLog(st, count));
            }
            catch (Exception e)
            {
                transaction.RollbackAndDispose();
                _temporaryLogManager.Add(new BulkSaveTemporaryLog(e));
                Logger.LogCritical(e, "BulkSaveAsync");
                throw;
            }
        }

        public void PrintStatus(StringBuilder sb)
        {
            if (_scraperState == null)
                return;

            sb.AppendLine("{");
            sb.AppendLine($"\"start_time\":\"{_start}\",");
            sb.AppendLine($"\"block_range\":{BlockRange},");
            sb.AppendLine($"\"container_count\":{_container.Count},");
            sb.AppendLine($"\"start_block\":{_scraperState.BlockId},");
            sb.AppendLine("\"logs\":[");
            _temporaryLogManager.PrintLogs(sb);
            sb.AppendLine("],");
            _blockMiningService.PrintStatus(sb);
            sb.AppendLine("}");
        }
    }

    internal class ScraperState
    {
        public long BlockId { get; set; }
    }
}