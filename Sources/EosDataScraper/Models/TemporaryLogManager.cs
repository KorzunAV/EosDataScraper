using System;
using System.Collections.Generic;
using System.Text;

namespace EosDataScraper.Models
{
    internal class TemporaryLogManager
    {
        public int MaxCount = 100;
        private readonly Queue<ITemporaryLog> _stateLoad = new Queue<ITemporaryLog>();

        public void Add(ITemporaryLog log)
        {
            lock (_stateLoad)
            {
                _stateLoad.Enqueue(log);
                if (_stateLoad.Count > MaxCount)
                    _stateLoad.Dequeue();
            }
        }

        public void PrintLogs(StringBuilder sb)
        {
            lock (_stateLoad)
            {
                var count = _stateLoad.Count;
                foreach (var log in _stateLoad)
                {
                    sb.AppendLine($"\"{log.ToString()}\"{(--count == 0 ? string.Empty : ",")}");
                }
            }
        }
    }

    internal interface ITemporaryLog
    {
        string ToString();
    }

    internal class ScraperServiceTemporaryLog : ITemporaryLog
    {
        private readonly Exception _exception;
        private readonly DateTime _now;
        private readonly DateTime _start;
        private readonly long _fromBlock;
        private readonly long _count;

        private long ToBlock => _fromBlock + _count;
        private double ReadSeconds => (_now - _start).TotalSeconds;
        private double BlockPerSec => _count / ReadSeconds;
        private bool IsError => _exception != null;


        public ScraperServiceTemporaryLog(DateTime start, long fromBlock, int count)
        {
            _now = DateTime.Now;
            _start = start;
            _fromBlock = fromBlock;
            _count = count;
        }

        public ScraperServiceTemporaryLog(Exception exception, DateTime start, long fromBlock)
        {
            _now = DateTime.Now;
            _exception = exception;
            _start = start;
            _fromBlock = fromBlock;
        }

        string ITemporaryLog.ToString()
        {
            return IsError
                ? $"[{_start:G} | {_now:G}]({ReadSeconds:N1} sec) [{_fromBlock},] {_exception.Message}"
                : $"[{_start:G} | {_now:G}]({ReadSeconds:N1} sec) Read {_count} [{_fromBlock} > {ToBlock}] | {BlockPerSec:N1} b/s";
        }
    }

    internal class BulkSaveTemporaryLog : ITemporaryLog
    {
        private readonly Exception _exception;
        private readonly DateTime _now;
        private readonly DateTime _start;
        private readonly int _count;

        private double ReadSeconds => (_now - _start).TotalSeconds;
        private bool IsError => _exception != null;

        public BulkSaveTemporaryLog(DateTime start, int count)
        {
            _now = DateTime.Now;
            _start = start;
            _count = count;
        }

        public BulkSaveTemporaryLog(Exception exception)
        {
            _exception = exception;
        }

        string ITemporaryLog.ToString()
        {
            return IsError
                ? $"[{_start:G} | {_now:G}]({ReadSeconds:N1} sec) {_exception.Message}"
                : $"[{_start:G} | {_now:G}]({ReadSeconds:N1} sec) Insert {_count} | {_count / ReadSeconds:N1} i/s";
        }
    }
}
