using System;
using System.Reflection;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using ExRpc.Common.Protocol;
using NetEZ.Core.Client;
using NetEZ.Core.Server;
using NetEZ.Utility.Logger;
using ExRpc.ZKAgent;
using ExRpc.ZKAgent.Event;
using ExRpc.ZKAgent.Watcher;

namespace ExRpc.Common.Server
{
    public class RPCServerBase : IServer
    {
        private PerformanceRecorder _PerformanceRecorder = null;
        private long _TIdSeed = 0;
        protected string _ServerName = string.Empty;
        protected HostInfo _Host = null;
        protected Logger _Logger = null;
        protected RPCServerHost _ServerHost = null;
        protected ConcurrentDictionary<string, IServant> _ServantTable = new ConcurrentDictionary<string, IServant>();

        protected GridUri _GridUri = null;
        protected ClusterMode _ClusterMode = ClusterMode.None;
        protected ClusterNodeAgent _ZKAgent = null;
        protected volatile List<ClusterNodeInfo> _ClusterNodes = null;
        protected ConcurrentDictionary<int, string> _ClusterInstanceIdHashTable = new ConcurrentDictionary<int, string>();

        public NetEZ.Utility.Logger.Logger Logger { get { return _Logger; } }

        protected RPCServerBase()
        {
            _ServerName = this.GetType().Name;

            _PerformanceRecorder = new PerformanceRecorder();
        }

        private long GetTId()
        {
            return Interlocked.Increment(ref _TIdSeed);
        }

        /// <summary>
        /// 初始化assembly中的servant对象
        /// </summary>
        /// <returns></returns>
        protected virtual bool InitServantTable()
        {
            if (_ServerHost.ServantAsmbs == null || _ServerHost.ServantAsmbs.Count < 1)
                return false;

            int cnt = 0;
            foreach (Assembly asmb in _ServerHost.ServantAsmbs)
            {
                foreach (Type tp in asmb.ExportedTypes)
                {
                    if (tp.IsAbstract)
                        continue;

                    if (tp.GetInterface("ExRpc.Common.Server.IServant", false) != null)
                    {
                        //  以后增加允许启动指定servant，而不是全部servant

                        IServant servant = (IServant)Activator.CreateInstance(tp);

                        if (servant != null)
                        {
                            servant.SetLogger(_Logger);
                            RegisterServant(servant);
                            cnt++;
                        }
                    }
                }
            }

            return cnt > 0 ? true : false;
        }

        protected virtual IServant GetServantInstance(string servantName)
        {
            IServant servant = null;
            if (servantName == null || servantName.Length < 1)
                return servant;

            servantName = servantName.ToLower();
            if (_ServantTable.TryGetValue(servantName, out servant))
                return servant;

            return null;
        }

        /// <summary>
        /// Master-Slave模式下获取主机
        /// </summary>
        /// <returns></returns>
        protected ClusterNodeInfo GetMasterClusterNode()
        {
            if (_ClusterMode != ClusterMode.MasterSlave)
                return null;

            lock (this)
            {
                if (_ClusterNodes != null && _ClusterNodes.Count > 0)
                    return _ClusterNodes[0];
            }

            return null;
        }

        /// <summary>
        /// Master-Slave模式下（随机）获取Slave实例
        /// </summary>
        /// <returns></returns>
        protected ClusterNodeInfo GetSlaveClusterNode()
        {
            if (_ClusterMode != ClusterMode.MasterSlave)
                return null;

            lock (this)
            {
                if (_ClusterNodes != null && _ClusterNodes.Count > 1)
                {
                    Random rnd = new Random();
                    int idx = rnd.Next(_ClusterNodes.Count - 1);
                    return _ClusterNodes[idx + 1];
                }

            }

            return null;
        }

        /// <summary>
        /// 根据val求余值获取node实例,ClusterWithHash不适用
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        protected ClusterNodeInfo GetClusterNodeByMod(int val)
        {
            if (_ClusterMode != ClusterMode.Cluster)
                return null;

            lock (this)
            {
                if (_ClusterNodes != null && _ClusterNodes.Count > 0)
                {
                    int idx = val >= 0 ? val % _ClusterNodes.Count : (0 - val) % _ClusterNodes.Count;
                    return _ClusterNodes[idx];
                }
            }

            return null;
        }

        /// <summary>
        /// 根据val求余值获取node实例,ClusterWithHash不适用
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        protected ClusterNodeInfo GetClusterNodeByMod(long val)
        {
            if (_ClusterMode != ClusterMode.Cluster)
                return null;

            lock (this)
            {
                if (_ClusterNodes != null && _ClusterNodes.Count > 0)
                {
                    int idx = val >= 0 ? (int)(val % _ClusterNodes.Count) : (int)((0 - val) % _ClusterNodes.Count);
                    return _ClusterNodes[idx];
                }
            }

            return null;
        }

        /// <summary>
        /// 随机获取node实例，一般用于无状态集群
        /// </summary>
        /// <returns></returns>
        protected ClusterNodeInfo GetClusterNodeByRandom()
        {
            //  一般用于无状态集群
            lock (this)
            {
                if (_ClusterNodes != null && _ClusterNodes.Count > 0)
                {
                    Random rnd = new Random();
                    int idx = rnd.Next(_ClusterNodes.Count);
                    return _ClusterNodes[idx];
                }
            }

            return null;
        }

        /// <summary>
        /// 指定instanceId（zookeeper下的节点id），获取实例
        /// </summary>
        /// <param name="instanceId"></param>
        /// <returns></returns>
        protected ClusterNodeInfo GetClusterNodeByInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;

            lock (this)
            {
                if (_ClusterNodes != null && _ClusterNodes.Count > 0)
                {
                    foreach (ClusterNodeInfo node in _ClusterNodes)
                    {
                        if (string.Compare(node.InstanceNodeId, instanceId, true) == 0)
                            return node;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// ClusterWithHash模式下获取实例
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        protected ClusterNodeInfo GetClusterNodeByHash(long val)
        {
            if (_ClusterMode != ClusterMode.ClusterWithHash)
                return null;

            int valHash = (int)(val % GridDefines.CLUSTER_HASH_SIZE);

            string instanceId = string.Empty;
            _ClusterInstanceIdHashTable.TryGetValue(valHash, out instanceId);

            return GetClusterNodeByInstanceId(instanceId);
        }

        /// <summary>
        /// ClusterWithHash模式下获取实例
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        protected ClusterNodeInfo GetClusterNodeByHash(int val)
        {
            if (_ClusterMode != ClusterMode.ClusterWithHash)
                return null;

            int valHash = val % GridDefines.CLUSTER_HASH_SIZE;

            string instanceId = string.Empty;
            _ClusterInstanceIdHashTable.TryGetValue(valHash, out instanceId);

            return GetClusterNodeByInstanceId(instanceId);
        }

        /// <summary>
        /// 重建hash值-ClusterNodeInstanceId映射表
        /// </summary>
        private void RebuildNodeInstanceIdHashTable(List<ClusterNodeInfo> nodes)
        {
            if (nodes == null || nodes.Count < 1)
            {
                //  初始化或者所有节点宕机（所有节点宕机理论上不存在）
                for (int i = 0; i < GridDefines.CLUSTER_HASH_SIZE; i++)
                {
                    _ClusterInstanceIdHashTable.AddOrUpdate(i, "", (k, v) => "");
                }

                if (_ClusterNodes == null)
                    _ClusterNodes = new List<ClusterNodeInfo>();
                else
                    _ClusterNodes.Clear();

                return;
            }

            bool isEqual = false;
            bool isBigger = false;

            List<ClusterNodeInfo> newNodes = null;

            if (_ClusterNodes == null || _ClusterNodes.Count < 1)
            {
                //  新集合更大
                isBigger = true;
                newNodes = nodes;                           //  全是新节点
            }
            else
            {
                //  先挑出新节点
                newNodes = new List<ClusterNodeInfo>();
                newNodes.AddRange(nodes);
                int offset = 0;
                while (offset < newNodes.Count)
                {
                    if (_ClusterNodes.Exists(c => string.Compare(c.InstanceNodeId, newNodes[offset].InstanceNodeId, true) == 0))
                    {
                        newNodes.RemoveAt(offset);
                        continue;
                    }
                    offset++;
                }

                if (_ClusterNodes.Count == nodes.Count && newNodes.Count == 0)
                    isEqual = true;                         //  两个集合相等（理论上不存在）
                else if (_ClusterNodes.Count < nodes.Count && newNodes.Count == (nodes.Count - _ClusterNodes.Count))
                    isBigger = true;                    //  当前集合是新集合的完全子集
            }

            //  新旧集合相等，什么也不做（这种情况应该不存在）
            if (isEqual)
                return;

            if (!isBigger)
            {
                //  认为集合变小，说明有服务器宕机，这时有些节点失效了，需要重新映射hash值
                //  两个集合有交叉，互相不为对方的子集的情况理论上只有在极端情况下出现

                _ClusterNodes = nodes;

                for (int i = 0; i < GridDefines.CLUSTER_HASH_SIZE; i++)
                {
                    if (!string.IsNullOrEmpty(_ClusterInstanceIdHashTable[i]) && _ClusterNodes.Exists(c => string.Compare(c.InstanceNodeId, _ClusterInstanceIdHashTable[i], true) == 0))
                        continue;           //  节点值依然有效

                    //  当前索引对应的节点失效,使用新值覆盖
                    if (newNodes != null && newNodes.Count > 0)
                    {
                        //  如果有新节点，则优先分配到新节点
                        ClusterNodeInfo nodeTmp = newNodes[i % newNodes.Count];
                        _ClusterInstanceIdHashTable.AddOrUpdate(i, nodeTmp.InstanceNodeId, (k, v) => nodeTmp.InstanceNodeId);
                    }
                    else
                    {
                        //  没有新节点（新的集合是当前集合的完全子集）则分配到现有节点
                        ClusterNodeInfo nodeTmp = _ClusterNodes[i % _ClusterNodes.Count];
                        _ClusterInstanceIdHashTable.AddOrUpdate(i, nodeTmp.InstanceNodeId, (k, v) => nodeTmp.InstanceNodeId);
                    }
                }
            }
            else
            {
                //  集合变大，说明有新的节点加入，需要分流一部分流量

                _ClusterNodes = nodes;

                for (int i = 0; i < GridDefines.CLUSTER_HASH_SIZE; i++)
                {
                    if ((i % _ClusterNodes.Count) == 0 || string.IsNullOrEmpty(_ClusterInstanceIdHashTable[i]) || !_ClusterNodes.Exists(c => string.Compare(c.InstanceNodeId, _ClusterInstanceIdHashTable[i], true) == 0))
                    {
                        //  分流这个idx到新节点
                        ClusterNodeInfo nodeTmp = newNodes[i % newNodes.Count];
                        _ClusterInstanceIdHashTable.AddOrUpdate(i, nodeTmp.InstanceNodeId, (k, v) => nodeTmp.InstanceNodeId);
                    }
                }
            }
        }

        protected virtual void OnClusterNodeChangedHandler(List<ClusterNodeInfo> nodes)
        {
            if (_ClusterMode == ClusterMode.ClusterWithHash)
            {
                lock (this)
                {
                    RebuildNodeInstanceIdHashTable(nodes);
                }
            }
        }

        public string GetServerName()
        {
            return _ServerName;
        }

        /// <summary>
        /// 创建Server实例后执行的第一个方法
        /// </summary>
        /// <param name="svrHost"></param>
        public void RegisterServerHost(RPCServerHost svrHost)
        {
            _ServerHost = svrHost;
        }

        public void SetLogger(Logger logger)
        {
            _Logger = logger;
            _PerformanceRecorder.SetLogger(logger);
        }

        public void SetHostInfo(HostInfo host)
        {
            _Host = host;
        }

        public void Error(string msg)
        {
            if (_Logger != null)
                _Logger.Error(msg);
        }

        public void Debug(string msg)
        {
            if (_Logger != null)
                _Logger.Debug(msg);
        }

        public void Info(string msg)
        {
            if (_Logger != null)
                _Logger.Info(msg);
        }

        public virtual bool Start()
        {
            return InitServantTable();
        }

        public void Stop(int timeout) { }

        public virtual bool StartClusterNode(ClusterMode mode)
        {
            //  启动Cluster模式只能是 “MasterSlave/Cluster/ClusterWithHash” 三选一
            if (mode == ClusterMode.None)
                return false;

            _ClusterMode = mode;

            _GridUri = _ServerHost.GetServerGridUri(_ServerName);
            if (_GridUri == null || string.IsNullOrEmpty(_GridUri.Proj) || string.IsNullOrEmpty(_GridUri.Cluster))
                return false;

            if (mode == ClusterMode.ClusterWithHash)
            {
                RebuildNodeInstanceIdHashTable(null);
            }

            //  
            ClusterNodeInfo nodeInfo = new ClusterNodeInfo(_GridUri.Root, _GridUri.Proj, _GridUri.Cluster, _Host.ToString());
            nodeInfo.Host = _Host.Host;
            nodeInfo.Port = _Host.Port;
            _ZKAgent = new ClusterNodeAgent(nodeInfo, null, _Logger);
            //  注册节点变更事件
            _ZKAgent.RegisterNodeChangedHanlder(OnClusterNodeChangedHandler);

            if (!_ZKAgent.Start())
            {
                Error(string.Format("[StartGrid] failed: ZKAgent start failed."));
                return false;
            }

            return true;
        }

        public bool CallServantMethod(IRpcCallMethod callReq, TcpClientManager clmngr)
        {
            string servantName = callReq.__servant;
            if (servantName == null || servantName.Length < 1)
                return false;

            //servantName = servantName.ToLower();
            IServant servant = GetServantInstance(servantName);

            if (servant == null)
                return false;

            IRpcCallMethodReturn response = null;
            callReq.__tid = GetTId();


            Stopwatch watch = new Stopwatch();
            watch.Start();

            bool ret = servant.call_method(callReq, out response);
            if (ret && response != null)
            {
                response.__cb_id = callReq.__cb_id;
                response.__tid = callReq.__tid;

                _ServerHost.SendMessageToClient(clmngr, response);
            }
            else
            {
                RpcCallMethodReturnInvalid invalidResponse = new RpcCallMethodReturnInvalid();
                invalidResponse.__cb_id = callReq.__cb_id;
                invalidResponse.__tid = callReq.__tid;

                _ServerHost.SendMessageToClient(clmngr, invalidResponse);

                Error(string.Format("[servant.call_method] failed: {0}", callReq.__method));
            }

            watch.Stop();

            string uri = _GridUri != null ? _GridUri.ToString() : _ServerName;
            //  TotalMilliseconds * 1000
            _PerformanceRecorder.Increment(uri, _Host.ToString(), servantName, callReq.__method, 1, ret ? 0 : 1, (long)(watch.ElapsedTicks * 1000000F / Stopwatch.Frequency));

            watch = null;
            return ret;
        }

        public void RegisterServant(IServant servant)
        {
            if (servant == null)
                return;
            string servantName = servant.GetServantName().ToLower();

            _ServantTable.AddOrUpdate(servantName, servant, (k, v) => servant);
        }
    }
}
