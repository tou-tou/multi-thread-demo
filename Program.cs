using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MultiThreadingDemo;

class Program
{
    // 各テストの基本パラメータ
    const int ThreadCount = 100;
    const int CallsPerThread = 10000;
    const int TotalCalls = ThreadCount * CallsPerThread;

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== マルチスレッドカウンター LTデモ ===\n");

        PrintExecutionEnvironment();

        await Step1_ShowNotThreadSafe();
        await Step2_ShowWithLock();
        await Step3_ComparePerformance();
        await Step4_ShowLockFreeDataLoss();

        Console.WriteLine("=== デモ終了 ===");
        Console.WriteLine("\n完了！何かキーを押してください...");
        Console.ReadKey();
    }

    /// <summary>
    /// LTパート1：スレッドセーフでない実装が、いかに問題を起こすかを示す
    /// </summary>
    static async Task Step1_ShowNotThreadSafe()
    {
        PrintHeader("1. スレッドセーフでない実装の問題点");
        Console.WriteLine("10スレッドから各10,000回、合計100,000回の呼び出しを行ってみます。");
        Console.WriteLine("結果：例外が発生したり、カウントが期待値と大きくずれたりします。\n");

        // テスト前にGCを実行し、メモリ状態をクリーンにする
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(200); // 安定待ち


        var counter = new MethodCounter_NotThreadSafe();
        long exceptionCount = 0;

        var tasks = Enumerable.Range(0, ThreadCount).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < CallsPerThread; i++)
            {
                try
                {
                    counter.Record("TestMethod");
                }
                catch
                {
                    Interlocked.Increment(ref exceptionCount);
                }
            }
        }));

        await Task.WhenAll(tasks);

        Console.WriteLine($"  -> 期待したカウント: {TotalCalls:N0}");
        Console.WriteLine($"  -> 実際のカウント:   {counter.GetCountsAndReset()["TestMethod"]:N0}");
        Console.WriteLine($"  -> 例外の発生回数:   {exceptionCount:N0}");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n  [結論] この実装はマルチスレッド環境では全く信頼できません。\n");
        Console.ResetColor();
    }

    /// <summary>
    /// LTパート2：lockを使えば、問題を安全・確実に解決できることを示す
    /// </summary>
    static async Task Step2_ShowWithLock()
    {
        PrintHeader("2. 'lock'によるスレッドセーフな実装");
        Console.WriteLine("同じテストを、今度は'lock'で保護した実装に対して行います。");
        Console.WriteLine("結果：例外は発生せず、カウントは完全に正確になります。\n");

         // テスト前にGCを実行し、メモリ状態をクリーンにする
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(200); // 安定待ち

        var counter = new MethodCounter_WithLock();

        var tasks = Enumerable.Range(0, ThreadCount).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < CallsPerThread; i++)
            {
                counter.Record("TestMethod");
            }
        }));

        await Task.WhenAll(tasks);

        Console.WriteLine($"  -> 期待したカウント: {TotalCalls:N0}");
        Console.WriteLine($"  -> 実際のカウント:   {counter.GetCountsAndReset()["TestMethod"]:N0}");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n  [結論] 'lock'を使えば、安全で正確なカウンターを簡単に実装できます。\n");
        Console.ResetColor();
    }

    /// <summary>
    /// LTパート3：パフォーマンスが心配... そこでLock-Free版の登場
    /// </summary>
    static async Task Step3_ComparePerformance()
    {
        PrintHeader("3. パフォーマンスとLock-Free版の登場");
        Console.WriteLine("'lock'は確実ですが、スレッドが互いを待ち合うため性能が低下する可能性があります。");
        Console.WriteLine("そこで登場するのがLock-Free実装です。純粋な書き込み性能を比較してみましょう。\n");

        const int durationSeconds = 3;

        // --- Lock版のテスト ---
        // テスト前にGCを実行し、メモリ状態をクリーンにする
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(200); // 安定待ち

        var lockCounter = new MethodCounter_WithLock();
        long lockWrites = await RunWriteOnlyTest(lockCounter, durationSeconds);
        Console.WriteLine($"  -> ✅ Lock版:      {lockWrites / durationSeconds,15:N0} 件/秒");

        // --- Lock-free版のテスト ---
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(200);

        var lockFreeCounter = new MethodCounter_LockFree();
        long lockFreeWrites = await RunWriteOnlyTest(lockFreeCounter, durationSeconds);
        Console.WriteLine($"  -> ⚡ Lock-free版: {lockFreeWrites / durationSeconds,15:N0} 件/秒");

        double speedup = (double)lockFreeWrites / lockWrites;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  [結論] Lock-Free版はLock版の約{speedup:F2}倍、高速に書き込みが可能です！\n");
        Console.ResetColor();
    }

    /// <summary>
    /// LTパート4：しかし...Lock-Free版も完全にはスレッドセーフではない？
    /// </summary>
    static async Task Step4_ShowLockFreeDataLoss()
    {
        PrintHeader("4. Lock-Free版の落とし穴：データロスト");
        Console.WriteLine("Lock-Free版は高速ですが、書き込みと読み出しが特定のタイミングで重なると...");
        Console.WriteLine("高負荷をかけながら、書き込みと読み出しを同時に実行してみます。\n");

         // テスト前にGCを実行し、メモリ状態をクリーンにする
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(200); // 安定待ち

        var counter = new MethodCounter_LockFree();
        long totalWritten = 0;
        long totalRead = 0;
        var cts = new CancellationTokenSource();

        var writerTasks = Enumerable.Range(0, ThreadCount).Select(_ => Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                counter.Record("Event");
                Interlocked.Increment(ref totalWritten);
            }
        })).ToList(); // タスクを開始させる（Enumerableのままだと遅延実行になってしまう）

        var readerTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                var c = counter.GetCountsAndReset();
                Interlocked.Add(ref totalRead, c.Values.Sum());
                await Task.Delay(2);
            }
        });

        await Task.Delay(5000); // 5秒間テスト
        cts.Cancel();
        await Task.WhenAll(writerTasks.Append(readerTask));
        totalRead += counter.GetCountsAndReset().Values.Sum(); // 残りを回収

        Console.WriteLine($"  -> 書き込み総数: {totalWritten:N0}");
        Console.WriteLine($"  -> 読み取り総数: {totalRead:N0}");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  -> ロストした数: {totalWritten - totalRead:N0}");
        Console.WriteLine("\n  [結論] わずかですが、データがロストしてしまいました！\n");
        Console.ResetColor();
    }



    // ----- ヘルパーメソッド -----

    #region Helpers
    static void PrintHeader(string title)
    {
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"## {title}");
        Console.WriteLine("--------------------------------------------------");
    }

    static async Task<long> RunWriteOnlyTest(IMethodCounter counter, int durationSeconds)
    {
        long totalWritten = 0;
        var cts = new CancellationTokenSource();

        // 100種類の異なるキーを用意して、より現実的な負荷を再現する
        var keys = Enumerable.Range(0, 100).Select(i => $"Method_{i}").ToArray();

        var writerTasks = Enumerable.Range(0, ThreadCount)
            .Select(threadIndex => Task.Run(() =>
            {
                // 各スレッドが独自の乱数生成器を持つことで、キーの選択が競合しないようにする
                var random = new Random(threadIndex);

                while (!cts.IsCancellationRequested)
                {
                    // 用意したキーの中からランダムに一つ選んで記録する
                    var key = keys[random.Next(keys.Length)];
                    counter.Record(key);
                    Interlocked.Increment(ref totalWritten);
                }
            })).ToList();

        await Task.Delay(TimeSpan.FromSeconds(durationSeconds));
        cts.Cancel();
        await Task.WhenAll(writerTasks);
        return totalWritten;
    }

    /// <summary>
    /// 現在のプロセスが利用可能な論理プロセッサ数を表示する
    /// </summary>
    static void PrintExecutionEnvironment()
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("-- 実行環境情報 --");
        // Environment.ProcessorCountは、tasksetなどで制限された場合、その制限後の数を返す
        Console.WriteLine($"このプロセスが利用可能な論理プロセッサ数: {Environment.ProcessorCount}");
        Console.WriteLine("--------------------");
        Console.ResetColor();
    }
    
#endregion
}