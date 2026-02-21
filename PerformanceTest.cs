using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExRpc.Common;
using ExRpc.Common.Proxy;

namespace ExRpc.PerformanceTest
{
    /// <summary>
    /// 性能测试统计
    /// </summary>
    public class PerfStats
    {
        private long _totalRequests = 0;
        private long _successRequests = 0;
        private long _failedRequests = 0;
        private long _timeoutRequests = 0;
        private ConcurrentBag<long> _latencies = new ConcurrentBag<long>();

        public void IncrementTotal() => Interlocked.Increment(ref _totalRequests);
        public void IncrementSuccess() => Interlocked.Increment(ref _successRequests);
        public void IncrementFailed() => Interlocked.Increment(ref _failedRequests);
        public void IncrementTimeout() => Interlocked.Increment(ref _timeoutRequests);
        public void RecordLatency(long ms) => _latencies.Add(ms);

        public long TotalRequests => _totalRequests;
        public long SuccessRequests => _successRequests;
        public long FailedRequests => _failedRequests;
        public long TimeoutRequests => _timeoutRequests;

        public double SuccessRate => _totalRequests > 0
            ? (double)_successRequests / _totalRequests * 100
            : 0;

        public void Reset()
        {
            _totalRequests = 0;
            _successRequests = 0;
            _failedRequests = 0;
            _timeoutRequests = 0;
            _latencies = new ConcurrentBag<long>();
        }

        public void PrintReport()
        {
            var sorted = _latencies.OrderBy(x => x).ToArray();

            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("性能测试报告");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"{"总请求数:",-20} {TotalRequests,15:N0}");
            Console.WriteLine($"{"成功请求数:",-20} {SuccessRequests,15:N0}");
            Console.WriteLine($"{"失败请求数:",-20} {FailedRequests,15:N0}");
            Console.WriteLine($"{"超时请求数:",-20} {TimeoutRequests,15:N0}");
            Console.WriteLine($"{"成功率:",-20} {SuccessRate,14:F2}%");

            if (sorted.Length > 0)
            {
                Console.WriteLine(new string('-', 60));
                Console.WriteLine("响应时间分布 (毫秒):");
                Console.WriteLine($"{"  最小值:",-20} {sorted[0],15:N2}");
                Console.WriteLine($"{"  P50 (中位数):",-20} {GetPercentile(sorted, 50),15:F2}");
                Console.WriteLine($"{"  P75:",-20} {GetPercentile(sorted, 75),15:F2}");
                Console.WriteLine($"{"  P90:",-20} {GetPercentile(sorted, 90),15:F2}");
                Console.WriteLine($"{"  P95:",-20} {GetPercentile(sorted, 95),15:F2}");
                Console.WriteLine($"{"  P99:",-20} {GetPercentile(sorted, 99),15:F2}");
                Console.WriteLine($"{"  最大值:",-20} {sorted[sorted.Length - 1],15:N2}");
                Console.WriteLine($"{"  平均值:",-20} {sorted.Average(),15:F2}");
            }

            Console.WriteLine(new string('=', 60) + "\n");
        }

        private double GetPercentile(long[] sorted, int percentile)
        {
            int index = (int)Math.Ceiling(sorted.Length * percentile / 100.0) - 1;
            return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
        }
    }

    /// <summary>
    /// RPC 性能测试框架
    /// </summary>
    public abstract class RpcPerformanceTester
    {
        protected Communicator _communicator;
        protected PerfStats _stats = new PerfStats();
        protected string _testName;

        public RpcPerformanceTester(string testName, Communicator communicator)
        {
            _testName = testName;
            _communicator = communicator;
        }

        /// <summary>
        /// 子类实现具体的RPC调用逻辑
        /// </summary>
        protected abstract void ExecuteRpcCall();

        /// <summary>
        /// 单线程基准测试
        /// </summary>
        public void RunBaselineTest(int requestCount)
        {
            Console.WriteLine($"\n[{_testName}] 基准测试");
            Console.WriteLine($"单线程, {requestCount:N0} 请求\n");

            _stats.Reset();
            Stopwatch totalSw = Stopwatch.StartNew();

            for (int i = 0; i < requestCount; i++)
            {
                ExecuteSingleRequest();

                if ((i + 1) % 1000 == 0)
                    Console.Write($"\r进度: {i + 1:N0} / {requestCount:N0}");
            }

            totalSw.Stop();
            Console.WriteLine($"\r进度: {requestCount:N0} / {requestCount:N0}");

            double qps = requestCount * 1000.0 / totalSw.ElapsedMilliseconds;
            Console.WriteLine($"\n总耗时: {totalSw.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"QPS: {qps:F2}");
            _stats.PrintReport();
        }

        /// <summary>
        /// 并发负载测试
        /// </summary>
        public void RunLoadTest(int concurrency, int durationSeconds)
        {
            Console.WriteLine($"\n[{_testName}] 负载测试");
            Console.WriteLine($"并发数={concurrency}, 持续时间={durationSeconds}秒\n");

            _stats.Reset();
            CancellationTokenSource cts = new CancellationTokenSource();
            Task[] tasks = new Task[concurrency];

            Stopwatch totalSw = Stopwatch.StartNew();

            // 启动多个并发任务
            for (int i = 0; i < concurrency; i++)
            {
                tasks[i] = Task.Run(() => ContinuousRequestWorker(cts.Token));
            }

            // 实时显示进度
            Task progressTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                    double elapsed = totalSw.ElapsedMilliseconds / 1000.0;
                    double currentQps = _stats.TotalRequests / Math.Max(elapsed, 0.001);
                    Console.Write($"\r进度: {elapsed:F0}s / {durationSeconds}s | 当前QPS: {currentQps:F0} | 成功率: {_stats.SuccessRate:F2}%     ");
                }
            });

            // 运行指定时间
            Thread.Sleep(durationSeconds * 1000);
            cts.Cancel();

            // 等待所有任务完成
            Task.WaitAll(tasks);
            progressTask.Wait();
            totalSw.Stop();

            Console.WriteLine();

            double actualSeconds = totalSw.ElapsedMilliseconds / 1000.0;
            double avgQps = _stats.TotalRequests / actualSeconds;

            Console.WriteLine($"\n实际运行时间: {actualSeconds:F2} 秒");
            Console.WriteLine($"平均QPS: {avgQps:F2}");
            _stats.PrintReport();
        }

        /// <summary>
        /// 压力测试（逐步增加并发）
        /// </summary>
        public void RunStressTest(int[] concurrencyLevels, int durationPerLevel = 60)
        {
            Console.WriteLine($"\n[{_testName}] 压力测试");
            Console.WriteLine($"并发级别: {string.Join(", ", concurrencyLevels)}");
            Console.WriteLine($"每个级别持续: {durationPerLevel}秒\n");

            foreach (int concurrency in concurrencyLevels)
            {
                Console.WriteLine($"\n{'=',60}");
                Console.WriteLine($"压力级别: {concurrency} 并发");
                Console.WriteLine($"{'=',60}");

                RunLoadTest(concurrency, durationPerLevel);

                // 休息10秒再进入下一级别
                if (concurrency != concurrencyLevels[concurrencyLevels.Length - 1])
                {
                    Console.WriteLine("\n冷却中，等待10秒...\n");
                    Thread.Sleep(10000);
                }
            }
        }

        /// <summary>
        /// 持续发送请求的工作线程
        /// </summary>
        private void ContinuousRequestWorker(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                ExecuteSingleRequest();
            }
        }

        /// <summary>
        /// 执行单次 RPC 请求并记录统计
        /// </summary>
        private void ExecuteSingleRequest()
        {
            _stats.IncrementTotal();
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                ExecuteRpcCall();

                sw.Stop();
                _stats.IncrementSuccess();
                _stats.RecordLatency(sw.ElapsedMilliseconds);
            }
            catch (TimeoutException)
            {
                sw.Stop();
                _stats.IncrementTimeout();
            }
            catch (Exception ex)
            {
                sw.Stop();
                _stats.IncrementFailed();
                // 避免打印过多错误信息
                if (_stats.FailedRequests <= 10)
                {
                    Console.WriteLine($"\n请求失败: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 示例：Echo 测试（需要替换为实际的Proxy调用）
    /// </summary>
    public class EchoPerformanceTester : RpcPerformanceTester
    {
        // private YourServiceProxy _proxy;

        public EchoPerformanceTester(Communicator communicator)
            : base("Echo测试", communicator)
        {
            // _proxy = new YourServiceProxy(communicator, "rpc://cluster.proj.root");
        }

        protected override void ExecuteRpcCall()
        {
            // TODO: 替换为实际的RPC调用
            // var result = _proxy.Echo("test");

            // 模拟调用（实际使用时删除）
            Thread.Sleep(1); // 模拟1ms延迟
        }
    }

    /// <summary>
    /// 测试程序入口
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(@"
╔══════════════════════════════════════════════════════════╗
║              ExRpc 性能测试工具 v1.0                      ║
╚══════════════════════════════════════════════════════════╝
");

            try
            {
                // 创建 Communicator
                var config = new CommunicatorConfigure
                {
                    ConnectTimeout = 200,
                    SendTimeout = 3000,
                    WaitingACKTimeout = 9000,
                    ConnectionPoolSize = 100
                };

                var communicator = new Communicator(config);
                // communicator.Start(); // 如果需要

                // 创建测试实例
                var tester = new EchoPerformanceTester(communicator);

                // 显示菜单
                while (true)
                {
                    Console.WriteLine("\n请选择测试类型:");
                    Console.WriteLine("  1. 基准测试 (单线程, 10000请求)");
                    Console.WriteLine("  2. 负载测试 (100并发, 60秒)");
                    Console.WriteLine("  3. 压力测试 (逐步增加并发)");
                    Console.WriteLine("  4. 自定义测试");
                    Console.WriteLine("  0. 退出");
                    Console.Write("\n请输入选项 (0-4): ");

                    string choice = Console.ReadLine();

                    switch (choice)
                    {
                        case "1":
                            tester.RunBaselineTest(10000);
                            break;

                        case "2":
                            tester.RunLoadTest(100, 60);
                            break;

                        case "3":
                            int[] levels = { 10, 50, 100, 200, 500 };
                            tester.RunStressTest(levels, 30);
                            break;

                        case "4":
                            RunCustomTest(tester);
                            break;

                        case "0":
                            Console.WriteLine("\n感谢使用！");
                            return;

                        default:
                            Console.WriteLine("\n无效选项，请重新选择。");
                            break;
                    }

                    Console.WriteLine("\n按任意键继续...");
                    Console.ReadKey();
                    Console.Clear();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n错误: {ex.Message}");
                Console.WriteLine($"堆栈: {ex.StackTrace}");
            }
        }

        static void RunCustomTest(RpcPerformanceTester tester)
        {
            Console.Write("请输入并发数 (例如: 100): ");
            if (!int.TryParse(Console.ReadLine(), out int concurrency) || concurrency <= 0)
            {
                Console.WriteLine("无效的并发数");
                return;
            }

            Console.Write("请输入持续时间(秒) (例如: 60): ");
            if (!int.TryParse(Console.ReadLine(), out int duration) || duration <= 0)
            {
                Console.WriteLine("无效的持续时间");
                return;
            }

            tester.RunLoadTest(concurrency, duration);
        }
    }
}
