using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MultiThreadingDemo
{
    /// <summary>
    /// メソッド呼び出し回数を記録するカウンターのインターフェース
    /// </summary>
    public interface IMethodCounter
    {
        void Record(string methodName);
        Dictionary<string, int> GetCountsAndReset();
    }

    /// <summary>
    /// ❌ スレッドセーフではない実装（危険！）
    /// </summary>
    public class MethodCounter_NotThreadSafe : IMethodCounter
    {
        private Dictionary<string, int> _counts = new();

        public void Record(string methodName)
        {
            // 複数スレッドから同時にアクセスすると...
            if (_counts.ContainsKey(methodName))
                _counts[methodName]++;  // ← ここで例外やデータ破損が発生！
            else
                _counts[methodName] = 1;
        }

        public Dictionary<string, int> GetCountsAndReset()
        {
            var result = _counts;
            _counts = new Dictionary<string, int>();  // ← ここも危険！
            return result;
        }
    }

    /// <summary>
    /// ✅ lockを使ったスレッドセーフな実装（シンプルだが遅い）
    /// </summary>
    public class MethodCounter_WithLock : IMethodCounter
    {
        private readonly object _lock = new();
        private Dictionary<string, int> _counts = new();

        public void Record(string methodName)
        {
            lock (_lock)  // ← スレッドが順番待ちでボトルネック
            {
                if (_counts.ContainsKey(methodName))
                    _counts[methodName]++;
                else
                    _counts[methodName] = 1;
            }
        }

        public Dictionary<string, int> GetCountsAndReset()
        {
            lock (_lock)
            {
                var result = _counts;
                _counts = new Dictionary<string, int>();
                return result;
            }
        }
    }

    /// <summary>
    /// ⚡ lock-freeで高速なスレッドセーフ実装
    /// </summary>
    public class MethodCounter_LockFree : IMethodCounter
    {
        // スレッドセーフなキュー（内部でlock-freeアルゴリズム使用）
        private ConcurrentQueue<string> _events = new();

        /// <summary>
        /// メソッド呼び出しを記録（1秒間に数千回呼ばれる想定）
        /// </summary>
        public void Record(string methodName)
        {
            _events.Enqueue(methodName);  // lockなしで超高速！
            //Thread.Sleep(1);
        }

        /// <summary>
        /// 集計して結果を返す（数秒に1回呼ばれる想定）
        /// </summary>
        public Dictionary<string, int> GetCountsAndReset()
        {
            // キューをアトミックに新しいものと交換
            // この瞬間にRecordが呼ばれるとカウント漏れの可能性あり（仕様として許容）
            var currentQueue = Interlocked.Exchange(
                ref _events, 
                new ConcurrentQueue<string>()
            );

            // 集計処理（この時点でcurrentQueueは他スレッドから触られない）
            var counts = new Dictionary<string, int>();
            while (currentQueue.TryDequeue(out var methodName))
            {
                if (counts.ContainsKey(methodName))
                    counts[methodName]++;
                else
                    counts[methodName] = 1;
            }

            return counts;
        }
    }

    /// <summary>
    /// パフォーマンス測定用のヘルパークラス
    /// </summary>
    public static class PerformanceHelper
    {
        public static TimeSpan MeasureTime(Action action)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }
    }
}