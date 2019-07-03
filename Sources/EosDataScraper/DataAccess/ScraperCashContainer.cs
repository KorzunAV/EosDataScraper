using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EosDataScraper.Models;
using Npgsql;

namespace EosDataScraper.DataAccess
{
    public class ScraperCashContainer
    {
        readonly List<TokenAction> _tokenActions = new List<TokenAction>();
        readonly List<TransferAction> _transferActions = new List<TransferAction>();
        readonly List<DelayedTransaction> _delayedTransactions = new List<DelayedTransaction>();

        readonly Queue<BaseTable> _buf = new Queue<BaseTable>();

        public int Count;

        public void AddToBuffer(BaseTable table)
        {
            _buf.Enqueue(table);
        }

        public void FlushBuffer()
        {
            while (_buf.TryDequeue(out var table))
            {
                switch (table)
                {
                    case TokenAction typed:
                        _tokenActions.Add(typed);
                        break;
                    case TransferAction typed:
                        _transferActions.Add(typed);
                        break;
                    case DelayedTransaction typed:
                        _delayedTransactions.Add(typed);
                        break;
                }
                Count++;
            }
        }

        private void DoCopy<T>(NpgsqlConnection connection, long toBlockNum, List<T> set)
            where T : BaseTable
        {
            if (!set.Any())
                return;

            set.Sort();

            if (set[0].BlockNum > toBlockNum)
                return;

            var save = set.Where(i => i.BlockNum <= toBlockNum).ToArray();
            set.RemoveRange(0, save.Length);

            var cmd = save[0].CopyCommandText();
            using (var writer = connection.BeginBinaryImport(cmd))
            {
                foreach (var itm in save)
                {
                    itm.Import(writer);
                    Count--;
                }

                writer.Complete();
            }
        }

        private async Task DoPartitionCopy(NpgsqlConnection connection, long toBlockNum, List<TransferAction> set, CancellationToken token)
        {
            if (!set.Any())
                return;

            set.Sort();

            if (set[0].BlockNum > toBlockNum)
                return;

            var save = set.Where(i => i.BlockNum <= toBlockNum).ToArray();
            set.RemoveRange(0, save.Length);

            var parts = save.GroupBy(i => i.Timestamp.Date);

            foreach (var part in parts)
            {
                var cmd = part.First().CopyCommandText();

                await connection.CreateTransferPartitionIfNotExist(part.Key, token);

                using (var writer = connection.BeginBinaryImport(cmd))
                {
                    foreach (var itm in part)
                    {
                        itm.Import(writer);
                        Count--;
                    }

                    writer.Complete();
                }
            }
        }

        public async Task CommitAndDispose(NpgsqlConnection connection, long maxBlock, CancellationToken token)
        {
            DoCopy(connection, maxBlock, _tokenActions);
            DoCopy(connection, maxBlock, _delayedTransactions);
            await DoPartitionCopy(connection, maxBlock, _transferActions, token);
        }

        internal void Clear()
        {
            _delayedTransactions.Clear();
            _tokenActions.Clear();
            _transferActions.Clear();
            Count = 0;
        }
    }
}
