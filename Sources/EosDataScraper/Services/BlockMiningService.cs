using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ditch.Core;
using Ditch.EOS;
using Ditch.EOS.Models;
using EosDataScraper.DataAccess;
using EosDataScraper.Models;
using Newtonsoft.Json;
using Npgsql;

namespace EosDataScraper.Services
{
    public sealed class BlockMiningService
    {
        private readonly Random _rand = new Random(DateTime.Now.Millisecond);
        private readonly List<DownloadWorker> _workers = new List<DownloadWorker>();
        private const int MaxDownloadWorkerSetCount = 50;
        private List<NodeInfo> _nodes;
        private List<DownloadWorker> _workersAll;

        private long _lastIrreversibleBlockNum = -1;
        private int _workerCount;
        private int _taskCount;
        private byte _threadsCount = 100;
        public byte ThreadsCount
        {
            get => _threadsCount;
            set
            {
                if (value > 0)
                    _threadsCount = value;
            }
        }

        public delegate void CallbackDelegate(GetBlockResults result);
        public CallbackDelegate Callback;


        private void AddResult(OperationResult<GetBlockResults> operationResult, CancellationToken token)
        {
            if (operationResult.IsError)
            {
                AddTaskAsync(operationResult.Result.BlockNum, true, token);
            }
            else
            {
                Callback.Invoke(operationResult.Result);
                Interlocked.Decrement(ref _workerCount);
                Interlocked.Decrement(ref _taskCount);
            }
        }

        public async Task InitCashAsync(NpgsqlConnection connection, long startBlock, int count, CancellationToken token)
        {
            if (_nodes == null)
            {
                _nodes = await connection.GetAllNodeInfo(token)
                    .ConfigureAwait(false);
                _workersAll = _nodes
                    .Select(n => new DownloadWorker(n, this))
                    .ToList();
            }

            _workerCount = 0;

            List<DownloadWorker> allWorkers;
            if (_lastIrreversibleBlockNum > -1)
            {
                allWorkers = new List<DownloadWorker>();
                for (var i = 0; i < _nodes.Count; i++)
                {
                    if (_nodes[i].LastIrreversibleBlockNum > _lastIrreversibleBlockNum / 2)
                        allWorkers.Add(_workersAll[i]);
                }
            }
            else
            {
                allWorkers = _workersAll;
            }


            if (startBlock + count > _lastIrreversibleBlockNum)
            {
                Parallel.ForEach(allWorkers, (worker, state, i) => { worker.GetLastIrreversibleBlockNum(token).Wait(token); });
                _lastIrreversibleBlockNum = allWorkers.Max(w => w.Node.LastIrreversibleBlockNum);
            }

            lock (_workers)
            {
                _workers.Clear();

                foreach (var w in allWorkers
                    .Where(w => w.Node.LastIrreversibleBlockNum > startBlock)
                    .OrderByDescending(w => w.Node.Durability)
                    .ThenBy(n => n.Node.Velocity)
                    .Take(MaxDownloadWorkerSetCount))
                {
                    _workers.Add(w);
                }
            }
        }



        private async void AddTaskAsync(long blockNum, bool isRandomWorker, CancellationToken token)
        {
            var w = await GetDownloadWorkerAsync(isRandomWorker, token)
                .ConfigureAwait(false);

            await w.AddBlockAsync(blockNum, token);
        }

        public async Task StartAsync(long from, long to, CancellationToken token)
        {
            if (to > _lastIrreversibleBlockNum)
                to = _lastIrreversibleBlockNum;

            _taskCount = (int)(to - from + 1);
            for (var blockNum = from; blockNum <= to; blockNum++)
            {
                token.ThrowIfCancellationRequested();

                while (_workerCount > ThreadsCount)
                {
                    await Task.Delay(100, token);
                }

                var w = await GetDownloadWorkerAsync(token)
                    .ConfigureAwait(false);

                w.NoWaitAddBlockAsync(blockNum, token);
                Interlocked.Increment(ref _workerCount);
            }

            while (_workerCount > 0)
            {
                await Task.Delay(100, token);
            }
        }

        private readonly SemaphoreSlim _downloadSemaphore = new SemaphoreSlim(1);

        private Task<DownloadWorker> GetDownloadWorkerAsync(CancellationToken token)
        {
            return GetDownloadWorkerAsync(false, token);
        }

        private async Task<DownloadWorker> GetDownloadWorkerAsync(bool isRandomWorker, CancellationToken token)
        {
            const int maxThreadPerWorker = 5;
            await _downloadSemaphore.WaitAsync(token);
            DownloadWorker worker = null;

            try
            {
                do
                {
                    lock (_workers)
                    {
                        var freeWorkers = _workers
                            .Where(w => w != null && w.TaskCount < maxThreadPerWorker && (w.Workload < 500 || w.TaskCount == 0))
                            .ToArray();

                        if (freeWorkers.Any())
                        {
                            if (isRandomWorker)
                            {
                                var id = _rand.Next(0, freeWorkers.Length - 1);
                                worker = freeWorkers[id];
                            }
                            else
                            {
                                worker = freeWorkers
                                    .OrderBy(w => w.Workload)
                                    .FirstOrDefault();
                            }
                        }
                    }

                    if (worker == null)
                    {
                        await Task.Delay(100, token)
                            .ConfigureAwait(false);
                        continue;
                    }

                    return worker;
                } while (true);
            }
            finally
            {
                _downloadSemaphore.Release(1);
            }
        }

        public async Task UpdateNodeInfoAsync(NpgsqlConnection connection, CancellationToken token)
        {
            foreach (var node in _nodes)
            {
                await connection.UpdateAsync(node, token);
            }
        }

        public string PrintStatus(StringBuilder sb)
        {
            if (_workers == null)
                return string.Empty;

            DownloadWorker[] workers;
            lock (_workers)
            {
                workers = _workers.ToArray();
            }
            sb.AppendLine($"\"last_irreversible_block_num\":{_lastIrreversibleBlockNum},");
            sb.AppendLine($"\"task_in_queue\":{_taskCount},");
            sb.AppendLine($"\"worker_count\":{_workerCount},");
            sb.AppendLine("\"workers\":[");
            for (var i = 0; i < workers.Length; i++)
                sb.AppendLine($"{JsonConvert.SerializeObject(workers[i], Formatting.None)}{(i < workers.Length - 1 ? "," : string.Empty)}");
            sb.AppendLine("]");

            return sb.ToString();
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class DownloadWorker
        {
            private readonly OperationManager _operationManager;
            private readonly BlockMiningService _miningService;
            private int _taskCount;

            [JsonProperty("node_info")]
            public readonly NodeInfo Node;


            [JsonProperty("task_count")]
            public int TaskCount
            {
                get => _taskCount;
                private set => _taskCount = value;
            }

            public double Workload
            {
                get
                {
                    lock (Node)
                    {
                        return Node.Velocity * (TaskCount + 1);
                    }
                }
            }

            public DownloadWorker(NodeInfo node, BlockMiningService miningService)
            {
                _miningService = miningService;
                Node = node;
                var httpClient = new RepeatHttpClient
                {
                    Timeout = TimeSpan.FromSeconds(5)
                };
                _operationManager = new OperationManager(httpClient)
                {
                    ChainUrl = node.Url
                };
            }

            public async void NoWaitAddBlockAsync(long blockNum, CancellationToken token)
            {
                await AddBlockAsync(blockNum, token);
            }

            public async Task AddBlockAsync(long blockNum, CancellationToken token)
            {
                Interlocked.Increment(ref _taskCount);

                var start = DateTime.Now;
                var args = new GetBlockParams { BlockNumOrId = blockNum.ToString() };
                var result = await _operationManager.GetBlockAsync(args, token)
                    .ConfigureAwait(false);
                var end = DateTime.Now;

                if (!result.IsError && result.Result == null)
                {
                    result.Exception = new NullReferenceException();
                }

                if (result.IsError)
                {
                    result.Result = new GetBlockResults
                    {
                        BlockNum = (uint)blockNum
                    };
                }

                lock (Node)
                {
                    Node.Update(end - start, !result.IsError);
                }

                _miningService.AddResult(result, token);
                Interlocked.Decrement(ref _taskCount);
            }

            public async Task<long> GetLastIrreversibleBlockNum(CancellationToken token)
            {
                Interlocked.Increment(ref _taskCount);
                var start = DateTime.Now;

                var result = await _operationManager.GetInfoAsync(token)
                    .ConfigureAwait(false);

                var end = DateTime.Now;

                if (!result.IsError)
                {
                    if (result.Result.ChainId.Equals(NodeInfo.MainNet))
                    {
                        lock (Node)
                        {
                            Node.LastIrreversibleBlockNum = result.Result.LastIrreversibleBlockNum;
                        }
                    }
                }

                lock (Node)
                {
                    Node.Update(end - start, !result.IsError);
                }
                Interlocked.Decrement(ref _taskCount);

                lock (Node)
                {
                    return Node.LastIrreversibleBlockNum;
                }
            }
        }
    }
}
