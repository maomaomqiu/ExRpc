using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExRpc.Messaging.MQ
{
    internal class RabbitMQClientPool
    {
        const uint BUFFER_SIZE = 256;
        const uint MAX_COUNT = 1024;

        private string _XConfigSectionName = string.Empty;
        private int _Totals = 0;
        private uint _Max = MAX_COUNT;
        private int _Timeout = 5000;                //  请求超时，单位毫秒
        private int _Idles = 0;

        private DateTime _LastRecycleTime = DateTime.Now;
        /// <summary>
        /// RabbitMQClient栈
        /// </summary>
        private ConcurrentQueue<RabbitMQClient> _Pool = new ConcurrentQueue<RabbitMQClient>();
        //private ConcurrentStack<RabbitMQClient> _Pool = new ConcurrentStack<RabbitMQClient>();
        
        private uint _BufferSize = BUFFER_SIZE;

        public RabbitMQClientPool(string xcfgName, uint buffSize = BUFFER_SIZE, uint maxItems = MAX_COUNT)
        {
            _XConfigSectionName = xcfgName;
            _BufferSize = buffSize;
            _Max = maxItems;
        }

        /// <summary>
        /// 返回RabbitMQClient池中的 数量
        /// </summary>
        public int Count { get { return _Pool.Count; } }

        /// <summary>
        /// 空闲RabbitMQClient数量
        /// </summary>
        public int TotalIdles { get { return _Idles; } }

        /// <summary>
        /// 累计创建的对象数
        /// </summary>
        public int Totals
        {
            get { return _Totals; }
        }

        /// <summary>
        /// 弹出一个RabbitMQClient
        /// </summary>
        /// <returns></returns>
        public RabbitMQClient Pop(bool waitingWhenEmpty = true)
        {
            RabbitMQClient result = null;

            if (!_Pool.TryDequeue(out result))
            {
                
                if (_Totals >= _Max)
                {
                    //  队列没有元素，而且累计创建的对象数达到上限，此时必须等待可用对象
                    int retries = 0;
                    DateTime timeOut = DateTime.Now.AddMilliseconds(_Timeout);
                    while (waitingWhenEmpty && !_Pool.TryDequeue(out result) && _Totals >= _Max && DateTime.Now < timeOut)
                    {
                        if (retries ++ < 50)
                            Thread.Sleep(5);
                        else
                            Thread.Sleep(10);
                    }

                    if (result != null)
                    {
                        Interlocked.Decrement(ref _Idles);
                        return result;
                    }

                    //  不等待
                    if (!waitingWhenEmpty)
                        return null;

                    //  超时已到仍未有可用对象
                    if (_Totals >= _Max)
                        return null;
                    //Console.WriteLine("\r[{0}] Pop Retry({1}) success.\r", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), retries);
                }

                //  队列没有元素，而且累计创建的对象数未到上限，此时创建新的对象实例
                try
                {
                    result = new RabbitMQClient(_XConfigSectionName);
                    Interlocked.Increment(ref _Totals);
                }
                catch { }
            }
            else
                Interlocked.Decrement(ref _Idles);

            return result;
        }

        /// <summary>
        /// 放回RabbitMQClient到Pool里
        /// </summary>
        /// <param name="item">RabbitMQClient instance to add to the pool.</param>
        public void Push(RabbitMQClient item)
        {
            if (item == null)
            {
                Interlocked.Decrement(ref _Totals);
                return;
            }

            _Pool.Enqueue(item);
            Interlocked.Increment(ref _Idles);
        }

        public void Recycle()
        {
            //  20个以下就不回收了, 最短回收间隔15s
            if (_Totals <= 20 || _LastRecycleTime.AddSeconds(15) > DateTime.Now)
                return;

            double percent = 0.0;
            if (20 < _Totals && _Totals < 40)
            {
                percent = 0.9;
            }
            else if (40 <= _Totals && _Totals < 60)
            {
                percent = 0.8;
            }
            else if (60 <= _Totals && _Totals < 80)
            {
                percent = 0.7;
            }
            else if (80 <= _Totals && _Totals < 200)
            {
                percent = 0.6;
            }
            else //if (80 <= _Totals && _Totals < 200)
            {
                percent = 0.5;
            }

            //  如果低于阈值也不回收
            if (Count < _Totals * percent)
                return;

            RabbitMQClient client = null;
            int recycleItems = (int)(_Totals * (1 - percent));
            for (int i = 0; i < recycleItems; i++)
            {
                client = Pop(false);
                if (client != null)
                {
                    try { client.Close(); }
                    catch { }
                    finally { client = null; }
                    Push(client);
                }
            }

            _LastRecycleTime = DateTime.Now;
        }
    }

    public class RabbitMQClientFactory
    {
        private static ConcurrentDictionary<string, RabbitMQClientPool> _Pools = new ConcurrentDictionary<string, RabbitMQClientPool>();
        private static Thread _RecycleClientThread = null;
        private static bool _IsThreadRunning = false;

        static RabbitMQClientFactory()
        {
            _IsThreadRunning = true;
            _RecycleClientThread = new Thread(RecycleRunning);
            _RecycleClientThread.IsBackground = true;
            _RecycleClientThread.Start();
            while (!_RecycleClientThread.IsAlive)
                Thread.Sleep(3);
        }

        private static void RecycleRunning(object state)
        {
            List<string> keyBuf = new List<string>();
            RabbitMQClientPool pool = null;
            while (_IsThreadRunning)
            {
                Thread.Sleep(10000);

                try 
                {
                    if (_Pools.Count < 1)
                        continue;

                    keyBuf = _Pools.Keys.ToList();
                    if (keyBuf.Count < 1)
                        continue;

                    foreach (string key in keyBuf)
                    {
                        if (!_Pools.TryGetValue(key, out pool))
                            continue;

                        pool.Recycle();
                    }
                }
                catch { }
                finally { if (keyBuf.Count > 0) keyBuf.Clear(); }
                
            }
        }

        public static void GetStat(string xcfgName, out int totals, out int count, out int totalIdles)
        {
            totals = count = totalIdles = 0;
            xcfgName = xcfgName != null ? xcfgName.Trim().ToLower() : "";
            RabbitMQClientPool pool = null;
            if (_Pools.TryGetValue(xcfgName, out pool))
            {
                totals = pool.Totals;
                count = pool.Count;
                totalIdles = pool.TotalIdles;
            }
        }

        public static RabbitMQClient GetClient(string xcfgName)
        {
            RabbitMQClientPool pool = null;

            xcfgName = xcfgName != null ? xcfgName.Trim().ToLower() : "";
            if (xcfgName.Length < 1)
                return null;

            if (_Pools.TryGetValue(xcfgName, out pool))
            {
                return pool.Pop();
            }

            pool = new RabbitMQClientPool(xcfgName);
            _Pools.AddOrUpdate(xcfgName, pool, (k, v) => v);

            if (_Pools.TryGetValue(xcfgName, out pool))
            {
                return pool.Pop();
            }

            return null;
        }

        public static void DestroyClient(string xcfgName, RabbitMQClient item)
        {
            if (item != null)
            {
                try { item.Close(); }
                catch { }
                finally { item = null; }
            }

            ReleaseClient(xcfgName, item);
        }

        public static void ReleaseClient(string xcfgName, RabbitMQClient item)
        {
            RabbitMQClientPool pool = null;

            xcfgName = xcfgName != null ? xcfgName.Trim().ToLower() : "";
            if (xcfgName.Length < 1)
            {
                try { if (item != null) item.Close(); }
                catch { }
                finally { item = null; }
                return;
            }

            if (_Pools.TryGetValue(xcfgName, out pool))
            {
                pool.Push(item);
            }
            else
            {
                try { if (item != null) item.Close(); }
                catch { }
                finally { item = null; }
            }
        }
    }
}
