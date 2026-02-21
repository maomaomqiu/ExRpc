using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEZ.Utility.Configure;
using org.apache.zookeeper;
using Newtonsoft.Json;
using NetEZ.Utility.Logger;
using ExRpc.ZKAgent.Event;
using ExRpc.ZKAgent.Watcher;

namespace ExRpc.ZKAgent
{
    public class ZKClient:NetEZ.Utility.Logger.LoggerBO, IDisposable
    {
        protected bool _Disposed = false;
        protected ZKConnectInfo _ZKServerConnInfo = null;
        protected volatile ZooKeeper _ZKDriver = null;
        //protected volatile ZooKeeperNet.ZooKeeper _ZKDriver = null;
        protected ZKConnectionWatcher _ZKServerWatcher = null;
        protected AutoResetEvent _ConnectedEvent = new AutoResetEvent(false);
        protected Thread _KeepAliveThread = null;
        protected volatile bool _IsKeepAliveThreadRunnning = false;
        protected volatile bool _ZKInited = false;

        private readonly object _Lock = new object();

        public event OnZKConnected _OnZKConnectedEventHandler;
        public event OnZKDisConnected _OnZKDisconnectedEventHandler;

        public ZKClient() { }

        public ZKClient(ZKConnectInfo zkServerInfo, Logger logger = null)
        {
            InitZKAgent(zkServerInfo, logger);
        }

        ~ZKClient()
        {
            Dispose();
        }

        protected bool InitZKAgent(ZKConnectInfo zkServerInfo, Logger logger = null)
        {
            _ZKServerConnInfo = zkServerInfo;
            if (logger != null)
                SetLogger(logger);

            //  ZKServer Watcher
            _ZKServerWatcher = new ZKConnectionWatcher(OnZKServerConnectedEventHandler, OnZKServerDisconnectedEventHandler);
            _ZKServerWatcher.SetLogger(logger);

            _IsKeepAliveThreadRunnning = true;
            _KeepAliveThread = new Thread(KeepClientAliveThreadRunning);
            _KeepAliveThread.IsBackground = true;
            _KeepAliveThread.Start();
            while (!_KeepAliveThread.IsAlive)
                Thread.Sleep(3);
            return true;
        }

        private void KeepClientAliveThreadRunning(object state)
        {
            Thread.Sleep(5000);

            while (_IsKeepAliveThreadRunnning)
            {
                Thread.Sleep(1500);
                if (_ZKDriver != null)
                {
                    //Console.WriteLine("[State] {0}",_ZKDriver.State);
                    if (_ZKDriver.getState() == ZooKeeper.States.CLOSED)
                    //if (_ZKDriver.State == ZooKeeper.States.CLOSED)
                    {
                        //  异步方式尝试创建/恢复连接
                        ConnectToZKServer();
                    }
                }
                else
                {
                    //  如果已经初始化过，因为网络等原因与zookeeper断开连接，此时_ZKDriver=null,需要重新创建实例并连接
                    if (_ZKInited)
                        ConnectToZKServer(true);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed)
                return;

            if (disposing)
            {
                //清理托管资源
                Release();
            }

            _Disposed = true;
        }

        protected virtual void Release()
        {
            _IsKeepAliveThreadRunnning = false;
            try 
            {
                if (_KeepAliveThread != null)
                    _KeepAliveThread.Join(200);
            }
            catch { }
            finally { _KeepAliveThread = null; }

            ShutdownZKServerConnection();
        }

        protected virtual void ShutdownZKServerConnection()
        {
            if (_ZKDriver == null)
                return;

            try
            {
                //_ZKDriver.Dispose();
                //Console.WriteLine("ZKClient Disposed.");
            }
            catch { }
            finally
            {
                _ZKDriver = null;
            }
        }

        protected virtual void OnZKServerConnectedEventHandler(WatchedEvent evtStat)
        {
            _ConnectedEvent.Set();

            //Console.WriteLine(_ZKDriver.SessionId);

            LaunchEventZKConnected(evtStat);
        }

        protected virtual void LaunchEventZKConnected(WatchedEvent evtStat)
        {
            if (_OnZKConnectedEventHandler != null)
                _OnZKConnectedEventHandler(evtStat);
        }

        protected virtual void LaunchEventZKDisconnected(WatchedEvent evtStat)
        {
            if (_OnZKDisconnectedEventHandler != null)
                _OnZKDisconnectedEventHandler(evtStat);
        }

        protected virtual void OnZKServerDisconnectedEventHandler(WatchedEvent evtStat)
        {
            LaunchEventZKDisconnected(evtStat);
        }


        protected ZKReturn ConnectToZKServerWithRetry(bool syncMode, ref int retryTimes)
        {
            try
            {
                if (_ZKDriver != null)
                {
                    try { /*_ZKDriver.Dispose();*/ }
                    catch { }
                    finally { _ZKDriver = null; }
                }

                //_ZKDriver = new ZooKeeper(_ZKServerConnInfo.ConnectString, new TimeSpan(0, 0, 0, 0, _ZKServerConnInfo.ConnectTimeout), _ZKServerWatcher);
                _ZKDriver = new ZooKeeper(_ZKServerConnInfo.ConnectString, _ZKServerConnInfo.ConnectTimeout, _ZKServerWatcher);

                if (syncMode)
                {
                    //  同步创建连接模式，等待连接成功直至超时
                    //  同步模式下没有尝试N次的机制
                    _ConnectedEvent.WaitOne(_ZKServerConnInfo.ConnectTimeout);
                    if (_ZKDriver != null && _ZKDriver.getState() == ZooKeeper.States.CONNECTED)
                        return ZKReturn.ZKReturnSuccess;
                    else
                        return ZKReturn.ZKReturnNoConnection;
                }
                else
                    return ZKReturn.ZKReturnSuccess;
            }
            catch (Exception ex)
            {
                LogError(string.Format("[ConnectToZKServer] Exception: retryTimes={0}; Ex={1}-{2}", retryTimes, ex.Message, ex.StackTrace));
            }

            //  失败尝试次数上限源自name server里配置数据
            retryTimes++;
            if (retryTimes > _ZKServerConnInfo.ConnectRetryTimes)
            {
                LogError(string.Format("[ConnectToZKServer] failed with retry: retryTimes={0}", retryTimes));
                return new ZKReturn(Defines.ZK_ERR_CONNECT_FAILED_WITHRETRY, string.Format("连接ZKServer失败,已连接{0}次", retryTimes));
            }

            //  默认重试间隔100ms
            Thread.Sleep(100);

            return ConnectToZKServerWithRetry(syncMode, ref retryTimes);
        }

        //protected ZKReturn ConnectToZKServerWithRetry(bool syncMode, ref int retryTimes)
        //{
        //    try
        //    {
        //        if (_ZKDriver != null)
        //        {
        //            try { _ZKDriver.Dispose(); }
        //            catch { }
        //            finally { _ZKDriver = null; }
        //        }

        //        _ZKDriver = new ZooKeeper(_ZKServerConnInfo.ConnectString, new TimeSpan(0, 0, 0, 0, _ZKServerConnInfo.ConnectTimeout), _ZKServerWatcher);

        //        if (syncMode)
        //        {
        //            //  同步创建连接模式，等待连接成功直至超时
        //            //  同步模式下没有尝试N次的机制
        //            _ConnectedEvent.WaitOne(_ZKServerConnInfo.ConnectTimeout);
        //            if (_ZKDriver != null && _ZKDriver.State == ZooKeeper.States.CONNECTED)
        //                return ZKReturn.ZKReturnSuccess;
        //            else
        //                return ZKReturn.ZKReturnNoConnection;
        //        }
        //        else
        //            return ZKReturn.ZKReturnSuccess;
        //    }
        //    catch (Exception ex)
        //    {
        //        LogError(string.Format("[ConnectToZKServer] Exception: retryTimes={0}; Ex={1}-{2}", retryTimes, ex.Message, ex.StackTrace));
        //    }

        //    //  失败尝试次数上限源自name server里配置数据
        //    retryTimes++;
        //    if (retryTimes > _ZKServerConnInfo.ConnectRetryTimes)
        //    {
        //        LogError(string.Format("[ConnectToZKServer] failed with retry: retryTimes={0}", retryTimes));
        //        return new ZKReturn(Defines.ZK_ERR_CONNECT_FAILED_WITHRETRY, string.Format("连接ZKServer失败,已连接{0}次", retryTimes));
        //    }

        //    //  默认重试间隔100ms
        //    Thread.Sleep(100);

        //    return ConnectToZKServerWithRetry(syncMode, ref retryTimes);
        //}

        
        public ZKReturn ConnectToZKServer(bool syncMode = false)
        {
            if (_ZKDriver != null && _ZKDriver.getState() == ZooKeeper.States.CONNECTED)
                //if (_ZKDriver != null)
                return ZKReturn.ZKReturnSuccess;

            lock (this)
            {
                if (_ZKDriver != null && _ZKDriver.getState() == ZooKeeper.States.CONNECTED)
                    //if (_ZKDriver != null)
                    return ZKReturn.ZKReturnSuccess;

                if (_ZKDriver == null || _ZKDriver.getState() == ZooKeeper.States.CLOSED)
                {
                    //  只有这两种情况下需要尝试创建连接/恢复连接
                    int retryTimes = 0;
                    return ConnectToZKServerWithRetry(syncMode, ref retryTimes);
                }

                return ZKReturn.ZKReturnNoConnection;
            }
        }
        
        //public ZKReturn ConnectToZKServer(bool syncMode = false)
        //{
        //    if (_ZKDriver != null && _ZKDriver.State == ZooKeeper.States.CONNECTED)
        //        //if (_ZKDriver != null)
        //        return ZKReturn.ZKReturnSuccess;

        //    lock (this)
        //    {
        //        if (_ZKDriver != null && _ZKDriver.State == ZooKeeper.States.CONNECTED)
        //            //if (_ZKDriver != null)
        //            return ZKReturn.ZKReturnSuccess;

        //        if (_ZKDriver == null || _ZKDriver.State == ZooKeeper.States.CLOSED)
        //        {
        //            //  只有这两种情况下需要尝试创建连接/恢复连接
        //            int retryTimes = 0;
        //            return ConnectToZKServerWithRetry(syncMode, ref retryTimes);
        //        }

        //        return ZKReturn.ZKReturnNoConnection;
        //    }
        //}
        public void Dispose()
        {
            //  释放所有的资源
            Dispose(true);
            //不需要再调用本对象的Finalize方法
            GC.SuppressFinalize(this);
        }

        public void RegisterZKConnectiongEventHandler(OnZKConnected connHandler,OnZKDisConnected disconnHandler)
        {
            if (connHandler != null)
                _OnZKConnectedEventHandler += connHandler;

            if (disconnHandler != null)
                _OnZKDisconnectedEventHandler += disconnHandler;
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="watcher"></param>
        /// <param name="data"></param>
        /// <param name="syncMode">true=同步读取模式，同步指：在尝试创建/恢复连接时是否为同步模式</param>
        /// <returns></returns>
        public ZKReturn GetData(string path, org.apache.zookeeper.Watcher watcher, out byte[] data, bool syncMode = false)
        {
            org.apache.zookeeper.data.Stat stat = null;

            return GetData(path, watcher, out data, out stat, syncMode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="watcher"></param>
        /// <param name="data"></param>
        /// <param name="stat"></param>
        /// <param name="syncMode">true=同步读取模式，同步指：在尝试创建/恢复连接时是否为同步模式</param>
        /// <returns></returns>
        public ZKReturn GetData(string path, org.apache.zookeeper.Watcher watcher, out byte[] data,out org.apache.zookeeper.data.Stat stat, bool syncMode = false)
        {
            ZKReturn ret = null;
            stat = null;
            data = null;
            DataResult dataRes = null;

            ret = ConnectToZKServer(syncMode);
            if (!ret.IsSuccess)
            {
                return ret;
            }

            if (string.IsNullOrEmpty(path))
                return ZKReturn.ZKReturnInvalid;
            try
            {
                stat = new org.apache.zookeeper.data.Stat();
                if (watcher == null)
                {
                    dataRes = _ZKDriver.getDataAsync(path, false).Result;
                }
                else
                {
                    dataRes = _ZKDriver.getDataAsync(path, watcher).Result;
                }

                if (dataRes != null)
                {
                    data = dataRes.Data;
                    stat = dataRes.Stat;
                }
                

                if (data == null || data.Length < 1)
                    return new ZKReturn(Defines.ZK_ERR_READ_FAILED, "读取数据失败.");

                return ZKReturn.ZKReturnSuccess;
            }
            catch (Exception ex)
            {
                LogError(string.Format("[ReadData] Exception: path={0}; Ex={1}-{2}", path, ex.Message, ex.StackTrace));
            }

            return ZKReturn.ZKReturnFail;
        }
        

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="path"></param>
        ///// <param name="watcher"></param>
        ///// <param name="data"></param>
        ///// <param name="syncMode">true=同步读取模式，同步指：在尝试创建/恢复连接时是否为同步模式</param>
        ///// <returns></returns>
        //public ZKReturn GetData(string path, IWatcher watcher, out byte[] data, bool syncMode = false)
        //{
        //    Stat stat = null;

        //    return GetData(path, watcher, out data, out stat, syncMode);
        //}

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="path"></param>
        ///// <param name="watcher"></param>
        ///// <param name="data"></param>
        ///// <param name="stat"></param>
        ///// <param name="syncMode">true=同步读取模式，同步指：在尝试创建/恢复连接时是否为同步模式</param>
        ///// <returns></returns>
        //public ZKReturn GetData(string path, IWatcher watcher, out byte[] data, out Stat stat, bool syncMode = false)
        //{
        //    ZKReturn ret = null;
        //    stat = null;
        //    data = null;

        //    ret = ConnectToZKServer(syncMode);
        //    if (!ret.IsSuccess)
        //    {
        //        return ret;
        //    }

        //    if (string.IsNullOrEmpty(path))
        //        return ZKReturn.ZKReturnInvalid;
        //    try
        //    {
        //        stat = new Stat();
        //        if (watcher == null)
        //            data = _ZKDriver.GetData(path, false, stat);
        //        else
        //            data = _ZKDriver.GetData(path, watcher, stat);

        //        if (data == null || data.Length < 1)
        //            return new ZKReturn(Defines.ZK_ERR_READ_FAILED, "读取数据失败.");

        //        return ZKReturn.ZKReturnSuccess;
        //    }
        //    catch (Exception ex)
        //    {
        //        LogError(string.Format("[ReadData] Exception: path={0}; Ex={1}-{2}", path, ex.Message, ex.StackTrace));
        //    }

        //    return ZKReturn.ZKReturnFail;
        //}

        //public async Task<ZKReturn> ListChildren(string path, org.apache.zookeeper.Watcher watcher,out List<string> children)
        public async Task<List<string>> ListChildren(string path, org.apache.zookeeper.Watcher watcher)
        {
            ZKReturn ret = null;
            List<string> children = null;
            ChildrenResult childrenRes = null;

            if (string.IsNullOrEmpty(path))
            {
                //return ZKReturn.ZKReturnInvalid;
                return children;
            }

            ret = ConnectToZKServer(true);
            if (!ret.IsSuccess)
            {
                return children;
            }

            try 
            {
                IEnumerable<string> buf = null;
                if (watcher == null)
                    childrenRes = await _ZKDriver.getChildrenAsync(path, false);
                else
                    childrenRes = await _ZKDriver.getChildrenAsync(path, watcher);

                if (childrenRes != null)
                    buf = childrenRes.Children;

                if (buf != null && buf.Count() > 0)
                    children = buf.ToList();

                return children;
                //return ZKReturn.ZKReturnSuccess;
            }
            catch(Exception ex)
            {
                LogError(string.Format("[ListChildren] Exception: path={0}; Ex={1}-{2}", path, ex.Message, ex.StackTrace));
            }
            finally { }

            //return ZKReturn.ZKReturnFail;
            return null;
        }

        //public ZKReturn ListChildren(string path, IWatcher watcher, out List<string> children)
        //{
        //    ZKReturn ret = null;
        //    children = null;

        //    if (string.IsNullOrEmpty(path))
        //    {
        //        return ZKReturn.ZKReturnInvalid;
        //    }

        //    ret = ConnectToZKServer(true);
        //    if (!ret.IsSuccess)
        //    {
        //        return ret;
        //    }

        //    try
        //    {
        //        IEnumerable<string> buf = null;
        //        if (watcher == null)
        //            buf = _ZKDriver.GetChildren(path, false);
        //        else
        //            buf = _ZKDriver.GetChildren(path, watcher);

        //        if (buf != null && buf.Count() > 0)
        //            children = buf.ToList();

        //        return ZKReturn.ZKReturnSuccess;
        //    }
        //    catch (Exception ex)
        //    {
        //        LogError(string.Format("[ListChildren] Exception: path={0}; Ex={1}-{2}", path, ex.Message, ex.StackTrace));
        //    }
        //    finally { }

        //    return ZKReturn.ZKReturnFail;
        //}

        
        public ZKReturn Delete(string path,int version = -1)
        {
            if (string.IsNullOrEmpty(path))
            {
                return ZKReturn.ZKReturnInvalid;
            }

            try
            {
                //_ZKDriver.Delete(path,version);
                _ZKDriver.deleteAsync(path, version).RunSynchronously();
                return ZKReturn.ZKReturnSuccess;
            }
            catch (Exception ex)
            {
                LogError(string.Format("[Remove] Exception: path={0}; Ex={1}-{2}", path, ex.Message, ex.StackTrace));
            }
            finally { }

            return ZKReturn.ZKReturnFail;
        }
        
        //public ZKReturn Delete(string path, int version = -1)
        //{
        //    if (string.IsNullOrEmpty(path))
        //    {
        //        return ZKReturn.ZKReturnInvalid;
        //    }

        //    try
        //    {
        //        _ZKDriver.Delete(path, version);
        //        return ZKReturn.ZKReturnSuccess;
        //    }
        //    catch (Exception ex)
        //    {
        //        LogError(string.Format("[Remove] Exception: path={0}; Ex={1}-{2}", path, ex.Message, ex.StackTrace));
        //    }
        //    finally { }

        //    return ZKReturn.ZKReturnFail;
        //}

        
        public ZKReturn Create(string path,string nodeData, HashSet<org.apache.zookeeper.data.ACL> aclList,CreateMode mode,out string nodeInstanceId)
        {
            nodeInstanceId = string.Empty;

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(nodeData))
            {
                return ZKReturn.ZKReturnInvalid;
            }

            //  写操作不能尝试创建连接
            //ZKReturn ret = ConnectToZKServer(true);
            //if (!ret.IsSuccess)
            //    return ret;

            try 
            {
                if (aclList == null)
                {
                    aclList = new HashSet<org.apache.zookeeper.data.ACL>();
                    //ZKId fullAcl = new ZKId("auth", "admin:admin");
                    aclList.Add(new org.apache.zookeeper.data.ACL((int)org.apache.zookeeper.ZooDefs.Perms.ALL, new org.apache.zookeeper.data.Id("world", "anyone")));          //  临时方案
                    //aclList.Add(new ACL((int)Perms.ALL, new ZKId("ip", "127.0.0.1")));
                }
                byte[] buf = System.Text.Encoding.UTF8.GetBytes(nodeData);
                string result = _ZKDriver.createAsync(path, buf, aclList.ToList(), mode).Result;
                if (string.IsNullOrEmpty(result))
                {
                    return ZKReturn.ZKReturnFail;
                }

                int idx = result.LastIndexOf('/');
                nodeInstanceId = result.Substring(idx + 1);

                return ZKReturn.ZKReturnSuccess;
            }
            catch (Exception ex)
            {
                LogError(string.Format("[Create] Exception: path={0}; data={1}; Ex={2}-{3}", path, nodeData, ex.Message, ex.StackTrace));
            }
            finally { }

            return ZKReturn.ZKReturnFail;
        }

        //public ZKReturn Create(string path, string nodeData, HashSet<ACL> aclList, CreateMode mode, out string nodeInstanceId)
        //{
        //    nodeInstanceId = string.Empty;

        //    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(nodeData))
        //    {
        //        return ZKReturn.ZKReturnInvalid;
        //    }

        //    //  写操作不能尝试创建连接
        //    //ZKReturn ret = ConnectToZKServer(true);
        //    //if (!ret.IsSuccess)
        //    //    return ret;

        //    try
        //    {
        //        if (aclList == null)
        //        {
        //            aclList = new HashSet<ACL>();
        //            //ZKId fullAcl = new ZKId("auth", "admin:admin");
        //            aclList.Add(new ACL((int)Perms.ALL, new ZKId("world", "anyone")));          //  临时方案
        //            //aclList.Add(new ACL((int)Perms.ALL, new ZKId("ip", "127.0.0.1")));
        //        }
        //        byte[] buf = System.Text.Encoding.UTF8.GetBytes(nodeData);
        //        string result = _ZKDriver.Create(path, buf, aclList, mode);
        //        if (string.IsNullOrEmpty(result))
        //        {
        //            return ZKReturn.ZKReturnFail;
        //        }

        //        int idx = result.LastIndexOf('/');
        //        nodeInstanceId = result.Substring(idx + 1);

        //        return ZKReturn.ZKReturnSuccess;
        //    }
        //    catch (Exception ex)
        //    {
        //        LogError(string.Format("[Create] Exception: path={0}; data={1}; Ex={2}-{3}", path, nodeData, ex.Message, ex.StackTrace));
        //    }
        //    finally { }

        //    return ZKReturn.ZKReturnFail;
        //}

        
        public ZKReturn SetData(string path, string nodeData,int version = -1)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(nodeData))
                return ZKReturn.ZKReturnInvalid;

            //  写操作不能尝试创建连接
            //ZKReturn ret = ConnectToZKServer(true);
            //if (!ret.IsSuccess)
            //    return ret;

            try 
            {
                byte[] buf = System.Text.Encoding.UTF8.GetBytes(nodeData);
                org.apache.zookeeper.data.Stat stat = _ZKDriver.setDataAsync(path, buf, version).Result;
                return ZKReturn.ZKReturnSuccess;
            }
            catch (Exception ex)
            {
                LogError(string.Format("[SetData] Exception: path={0}; data={1}; Ex={2}-{3}", path,nodeData, ex.Message, ex.StackTrace));
            }
            finally { }

            return ZKReturn.ZKReturnFail;
        }

        //public ZKReturn SetData(string path, string nodeData, int version = -1)
        //{
        //    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(nodeData))
        //        return ZKReturn.ZKReturnInvalid;

        //    //  写操作不能尝试创建连接
        //    //ZKReturn ret = ConnectToZKServer(true);
        //    //if (!ret.IsSuccess)
        //    //    return ret;

        //    try
        //    {
        //        byte[] buf = System.Text.Encoding.UTF8.GetBytes(nodeData);
        //        Stat stat = _ZKDriver.SetData(path, buf, version);
        //        return ZKReturn.ZKReturnSuccess;
        //    }
        //    catch (Exception ex)
        //    {
        //        LogError(string.Format("[SetData] Exception: path={0}; data={1}; Ex={2}-{3}", path, nodeData, ex.Message, ex.StackTrace));
        //    }
        //    finally { }

        //    return ZKReturn.ZKReturnFail;
        //}

        
        public ZKReturn WatchChildren(string path, ChildrenChangedWatcher watcher)
        {
            List<string> nodeIdList = null;
            return WatchChildren(path, watcher, out nodeIdList);
        }

        public ZKReturn WatchChildren(string path, ChildrenChangedWatcher watcher, out List<string> nodeIdList)
        {
            nodeIdList = null;
            if (watcher == null)
                return ZKReturn.ZKReturnInvalid;

            try
            {
                ChildrenResult childrenResult = _ZKDriver.getChildrenAsync(path, watcher).Result;
                if (childrenResult != null)
                    nodeIdList = childrenResult.Children;
                
                return ZKReturn.ZKReturnSuccess;
            }
            catch (Exception ex)
            {
                LogError(string.Format("[WatchChildren] Exception: path={0}; Ex={1}-{2}", path, ex.Message, ex.StackTrace));
            }
            finally { }

            return ZKReturn.ZKReturnFail;
        }
        
        //public ZKReturn WatchChildren(string path, ChildrenChangedWatcher watcher)
        //{
        //    List<string> nodeIdList = null;
        //    return WatchChildren(path, watcher, out nodeIdList);
        //}

        //public ZKReturn WatchChildren(string path, ChildrenChangedWatcher watcher, out List<string> nodeIdList)
        //{
        //    nodeIdList = null;
        //    if (watcher == null)
        //        return ZKReturn.ZKReturnInvalid;

        //    try
        //    {
        //        IEnumerable<string> childrenBuf = _ZKDriver.GetChildren(path, watcher);
        //        if (childrenBuf != null && childrenBuf.Count() > 0)
        //        {
        //            nodeIdList = childrenBuf.ToList();
        //        }

        //        return ZKReturn.ZKReturnSuccess;
        //    }
        //    catch (Exception ex)
        //    {
        //        LogError(string.Format("[WatchChildren] Exception: path={0}; Ex={1}-{2}", path, ex.Message, ex.StackTrace));
        //    }
        //    finally { }

        //    return ZKReturn.ZKReturnFail;
        //}
    }
}
