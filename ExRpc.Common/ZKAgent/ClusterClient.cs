using System;
using System.Threading;
using System.Collections.Concurrent;
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
using ExRpc;
using ExRpc.Common;

namespace ExRpc.ZKAgent
{
    public class ClusterClient:ZKClient,IDisposable
    {
        const string PATH_PREFIX_ROOT = "/proj-cpc/{0}/{1}/register";

        protected string _ClusterPathBase = string.Empty;

        protected string _GridRoot = string.Empty;
        protected string _ProjName = string.Empty;
        protected string _ClusterName = string.Empty;
        
        protected ClusterMode _ClusterMode = ClusterMode.None;
        protected List<ClusterNodeInfo> _ClusterNodes = null;
        protected ChildrenChangedWatcher _OnClusterNodesChangedWatcher = null;
        protected event OnClusterNodeChanged _OnClusterNodeChangedHanlder = null;
        protected ConcurrentDictionary<int, string> _ClusterInstanceIdHashTable = new ConcurrentDictionary<int, string>();
        protected CommunicatorConfigure _ClusterCommConfig = null;
        
        public CommunicatorConfigure CommunicatorConfig { get { return _ClusterCommConfig; } }

        public ClusterClient(string rootName, string projName, string clusterName, ClusterMode mode, ZKConnectInfo zkConn = null, Logger logger = null)
        {
            SetLogger(logger);

            _ProjName = projName != null ? projName.Trim().ToLower() : "";
            _ClusterName = clusterName != null ? clusterName.Trim().ToLower() : "";
            _GridRoot = rootName != null ? rootName.Trim().ToLower() : "";

            if (_ProjName.Length < 1 || _ClusterName.Length < 1 || _GridRoot.Length < 1)
                throw new Exception("project-name and cluster-name must be supplied.");

            _ClusterMode = mode;
            _ClusterPathBase = string.Format(PATH_PREFIX_ROOT, _ProjName, _ClusterName);
            
        }

        public ClusterClient(string projName, string clusterName, ZKConnectInfo zkConn = null, Logger logger = null)
        {
            SetLogger(logger);

            _ProjName = projName != null ? projName.Trim().ToLower() : "";
            _ClusterName = clusterName != null ? clusterName.Trim().ToLower() : "";

            if (_ProjName.Length < 1 || _ClusterName.Length < 1)
                throw new Exception("project-name and cluster-name must be supplied.");

            _GridRoot = "wxroot";             //  默认网格根路径

            _ClusterPathBase = string.Format(PATH_PREFIX_ROOT, _ProjName, _ClusterName);
        }

        ~ClusterClient()
        {
            Dispose();
        }

        //public override void Dispose()
        //{
        //    //  释放所有的资源
        //    Dispose(true);
        //    //不需要再调用本对象的Finalize方法
        //    GC.SuppressFinalize(this);
        //}

        public void RegisterNodeChangedHanlder(OnClusterNodeChanged handler)
        {
            if (handler != null)
                _OnClusterNodeChangedHanlder += handler;
        }

        public bool Start(ZKConnectInfo zkConn = null)
        {
            try
            {
                if (zkConn == null)
                {
                    string zkConfigPath = @"c:\xconfig2\zookeeper.xml";
                    NetEZ.Utility.Configure.SimpleXmlConfigure config = new SimpleXmlConfigure(zkConfigPath);
                    string zkHosts = config.GetItemValue(_GridRoot, 0);
                    zkConn = new ZKConnectInfo(zkHosts);
                }

                if (!InitZKAgent(zkConn))
                {
                    LogError(string.Format("[ClusterClient] starting failed(InitZKAgent return fail). proj={0}; cluster={1}", _ProjName, _ClusterName));
                    return false;
                }

                if (_ClusterMode == ClusterMode.ClusterWithHash)
                    RebuildNodeInstanceIdHashTable(null);
                //  同步模式连接到zk服务器
                ZKReturn zkRet = ConnectToZKServer(true);

                if (!zkRet.IsSuccess)
                {
                    LogError(string.Format("[ClusterClient] starting failed. ConnectToZKServer return={0}-{1}; ", zkRet.Code, zkRet.Msg));
                    return false;
                }

                //  读取cluster communicator配置信息
                zkRet = GetClusterCommConfigure();
                if (!zkRet.IsSuccess)
                {
                    LogError(string.Format("[ClusterClient] starting failed. GetClusterCommConfigure return={0}-{1}; ", zkRet.Code, zkRet.Msg));
                    return false;
                }

                _ZKInited = true;

                return true;
            }
            catch (Exception ex)
            {
                LogError(string.Format("[ClusterNodeAgent] starting failed. Ex={0};{1}", ex.Message, ex.StackTrace));
            }
            finally { }

            return false;
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
                string nodeIdTmp = string.Empty;
                for (int i = 0; i < GridDefines.CLUSTER_HASH_SIZE; i++)
                {
                    _ClusterInstanceIdHashTable.TryGetValue(i, out nodeIdTmp);
                    if (!string.IsNullOrEmpty(nodeIdTmp) && _ClusterNodes.Exists(c => string.Compare(c.InstanceNodeId, nodeIdTmp, true) == 0))
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
                string nodeIdTmp = string.Empty;
                for (int i = 0; i < GridDefines.CLUSTER_HASH_SIZE; i++)
                {
                    _ClusterInstanceIdHashTable.TryGetValue(i, out nodeIdTmp);

                    if ((i % _ClusterNodes.Count) == 0 || string.IsNullOrEmpty(nodeIdTmp) || !_ClusterNodes.Exists(c => string.Compare(c.InstanceNodeId, nodeIdTmp, true) == 0))
                    {
                        //  分流这个idx到新节点
                        ClusterNodeInfo nodeTmp = newNodes[i % newNodes.Count];
                        _ClusterInstanceIdHashTable.AddOrUpdate(i, nodeTmp.InstanceNodeId, (k, v) => nodeTmp.InstanceNodeId);
                    }
                }
            }
        }

        private void OnClusterNodesChangedEventHandler(WatchedEvent evtStat)
        {
            //WatchChildren(evtStat.Path);
            GetClusterNodes();
        }

        protected override void OnZKServerDisconnectedEventHandler(WatchedEvent evtStat)
        {
            _OnClusterNodesChangedWatcher = null;
            try
            {
                //_ZKDriver.Dispose();
            }
            catch { }
            finally { _ZKDriver = null; }
            base.OnZKServerDisconnectedEventHandler(evtStat);
        }

        protected override void OnZKServerConnectedEventHandler(WatchedEvent evtStat)
        {
            //  调用基类方法
            base.OnZKServerConnectedEventHandler(evtStat);

            //  读取所有节点（并添加watcher），必须成功
            ZKReturn ret = ZKReturn.ZKReturnFail;
            while (!ret.IsSuccess)
            {
                ret = GetClusterNodes();
                if (!ret.IsSuccess)
                {
                    Thread.Sleep(500);
                }
            }
        }

        private ZKReturn GetClusterCommConfigure()
        { 
            byte[] buf = null;
            ZKReturn ret = GetData(_ClusterPathBase, null, out buf, true);
            if (!ret.IsSuccess || buf == null || buf.Length < 1)
            {
                LogError(string.Format("[GetClusterCommConfigure] failed. GetData:{0}", ret.Code));
                return ret;
            }

            try 
            {
                string dataJson = Encoding.UTF8.GetString(buf);
                _ClusterCommConfig = JsonConvert.DeserializeObject<CommunicatorConfigure>(dataJson);

                if (_ClusterCommConfig != null)
                    return ZKReturn.ZKReturnSuccess;
                else
                {
                    LogError(string.Format("[GetClusterCommConfigure] failed. CommunicatorConfigure is null"));
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format("[GetClusterCommConfigure] Exception: Ex={0}-{1}", ex.Message, ex.StackTrace));
            }

            return ZKReturn.ZKReturnFail;
        }

        /// <summary>
        /// 获取全部节点
        /// </summary>
        /// <returns></returns>
        public ZKReturn GetClusterNodes()
        {
            List<ClusterNodeInfo> nodes = null;
            byte[] buf = null;
            List<string> nodeIdList = null;

            lock (this)
            {
                if (_OnClusterNodesChangedWatcher == null)
                    _OnClusterNodesChangedWatcher = new ChildrenChangedWatcher(OnClusterNodesChangedEventHandler);

                
                //ZKReturn ret = ListChildren(_ClusterPathBase, _OnClusterNodesChangedWatcher, out nodeIdList);
                nodeIdList = ListChildren(_ClusterPathBase, _OnClusterNodesChangedWatcher).Result;
                //if (!ret.IsSuccess)
                //{
                //    LogError(string.Format("[GetClusterNodes] failed. ret={0}; path={1}", ret.Code, _ClusterPathBase));
                //    return ret;
                //}

                //if (nodeIdList == null || nodeIdList.Count < 1)
                //{
                //    LogInfo(string.Format("[GetClusterNodes] nodes list is empty. path={0}", _ClusterPathBase));
                //    return ZKReturn.ZKReturnFail;
                //    //return ret;
                //}

                if (nodeIdList != null && nodeIdList.Count > 0)
                {
                    foreach (string nodeId in nodeIdList)
                    {
                        //  逐一读取每个节点数据
                        string path = string.Format("{0}/{1}", _ClusterPathBase, nodeId);
                        try
                        {
                            ZKReturn ret = GetData(path, null, out buf, true);
                            if (!ret.IsSuccess || buf == null || buf.Length < 1)
                            {
                                //  有可能读不到节点数据，比如该节点离线了
                                LogInfo(string.Format("[GetClusterNodes] GetData failed. ret={0}; path={1}", ret.Code, path));
                                continue;
                            }

                            string nodeData = System.Text.Encoding.UTF8.GetString(buf);
                            ClusterNodeInfo nodeInfo = JsonConvert.DeserializeObject<ClusterNodeInfo>(nodeData);
                            if (nodes == null)
                                nodes = new List<ClusterNodeInfo>();

                            nodeInfo.SetIntanceNodeId(nodeId);
                            nodes.Add(nodeInfo);
                        }
                        catch (Exception ex)
                        {
                            LogError(string.Format("[GetClusterNodes] GetData Exception. path={0}; Ex={1}-{2}", path, ex.Message, ex.StackTrace));
                            continue;
                        }
                        finally { }
                    }
                }
                    

                if (nodes != null && nodes.Count > 0)
                {
                    //  强制按instanceNodeId排序
                    nodes = nodes.OrderBy(c => c.InstanceNodeId).ToList();
                }

                if (_ClusterMode == ClusterMode.ClusterWithHash)
                {
                    lock (this)
                    {
                        RebuildNodeInstanceIdHashTable(nodes);
                        //int i = 1;
                    }
                }
                else
                {
                    _ClusterNodes = nodes;
                }
                //Console.WriteLine("[GetClusterNodes] nodes={0}; #{1}#", nodes != null ? nodes.Count : 0, DateTime.Now.ToString("HH:mm:ss"));
            }

            if (_OnClusterNodeChangedHanlder != null)
                _OnClusterNodeChangedHanlder(_ClusterNodes);

            return ZKReturn.ZKReturnSuccess;
        }

        /// <summary>
        /// Master-Slave模式下获取主机
        /// </summary>
        /// <returns></returns>
        public ZKReturn GetMasterClusterNode(out ClusterNodeInfo nodeInfo)
        {
            nodeInfo = null;
            if (_ClusterMode != ClusterMode.MasterSlave)
                return ZKReturn.ZKReturnInvalid;

            lock (this)
            {
                if (_ClusterNodes != null && _ClusterNodes.Count > 0)
                { 
                    nodeInfo = _ClusterNodes[0];
                    return ZKReturn.ZKReturnSuccess;
                }
            }

            return ZKReturn.ZKReturnFail;
        }

        /// <summary>
        /// Master-Slave模式下（随机）获取Slave实例
        /// </summary>
        /// <returns></returns>
        public ZKReturn GetSlaveClusterNode(out ClusterNodeInfo nodeInfo)
        {
            nodeInfo = null;
            if (_ClusterMode != ClusterMode.MasterSlave)
                return ZKReturn.ZKReturnInvalid;

            lock (this)
            {
                if (_ClusterNodes != null && _ClusterNodes.Count > 1)
                {
                    Random rnd = new Random();
                    int idx = rnd.Next(_ClusterNodes.Count - 1);
                    nodeInfo = _ClusterNodes[idx + 1];
                    return ZKReturn.ZKReturnSuccess;
                }

            }

            return ZKReturn.ZKReturnFail;
        }

        /// <summary>
        /// 随机获取node实例，一般用于无状态集群或无状态模式写操作
        /// </summary>
        /// <returns></returns>
        public ZKReturn GetClusterNodeByRandom(out ClusterNodeInfo nodeInfo)
        {
            //  一般用于无状态集群
            lock (this)
            {
                if (_ClusterNodes != null && _ClusterNodes.Count > 0)
                {
                    Random rnd = new Random();
                    int idx = rnd.Next(_ClusterNodes.Count);
                    nodeInfo = _ClusterNodes[idx];
                    return ZKReturn.ZKReturnSuccess;
                }
            }

            nodeInfo = null;
            return null;
        }


        /// <summary>
        /// 使用指定数值的求余值获取实例
        /// </summary>
        /// <param name="val"></param>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public ZKReturn GetClusterNodeByMod(long val, out ClusterNodeInfo nodeInfo)
        {
            nodeInfo = null;
            if (_ClusterNodes == null || _ClusterNodes.Count < 1)
                return ZKReturn.ZKReturnFail;

            if (val < 0)
                val = 0 - val;

            try
            {
                int idx = val >= 0 ? (int)(val % _ClusterNodes.Count) : (int)((0 - val) % _ClusterNodes.Count);
                nodeInfo = _ClusterNodes[idx];
                return ZKReturn.ZKReturnSuccess;
            }
            catch { }

            return ZKReturn.ZKReturnFail;
        }

        /// <summary>
        /// 使用指定数值的求余值获取实例
        /// </summary>
        /// <param name="val"></param>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public ZKReturn GetClusterNodeByMod(int val, out ClusterNodeInfo nodeInfo)
        { 
            nodeInfo = null;
            if (_ClusterNodes == null || _ClusterNodes.Count < 1)
                return ZKReturn.ZKReturnFail;

            if (val < 0)
                val = 0 - val;

            try 
            {
                int idx = val >= 0 ? val % _ClusterNodes.Count : (0 - val) % _ClusterNodes.Count;
                nodeInfo = _ClusterNodes[idx];
                return ZKReturn.ZKReturnSuccess;
            }
            catch { }

            return ZKReturn.ZKReturnFail;
        }

        /// <summary>
        /// 指定InstanceId获取Node实例
        /// </summary>
        /// <param name="nodeInstanceId"></param>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public ZKReturn GetClusterNodeById(string nodeInstanceId, out ClusterNodeInfo nodeInfo)
        {
            nodeInfo = null;
            if (_ClusterNodes == null || _ClusterNodes.Count < 1)
                return ZKReturn.ZKReturnFail;

            try
            {
                foreach (ClusterNodeInfo node in _ClusterNodes)
                {
                    if (string.Compare(nodeInstanceId, nodeInfo.InstanceNodeId, true) == 0)
                    {
                        nodeInfo = node;
                        return ZKReturn.ZKReturnSuccess;
                    }
                }
            }
            catch { }

            return ZKReturn.ZKReturnFail;
        }

        /// <summary>
        /// ClusterWithHash模式下获取实例
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public ZKReturn GetClusterNodeByHash(long val, out ClusterNodeInfo nodeInfo)
        {
            if (_ClusterMode != ClusterMode.ClusterWithHash)
            {
                nodeInfo = null;
                return ZKReturn.ZKReturnInvalid;
            }

            int valHash = (int)(val % GridDefines.CLUSTER_HASH_SIZE);

            string instanceId = string.Empty;
            _ClusterInstanceIdHashTable.TryGetValue(valHash, out instanceId);

            nodeInfo = GetClusterNodeByInstanceId(instanceId);
            return nodeInfo != null ? ZKReturn.ZKReturnSuccess : ZKReturn.ZKReturnFail;
        }

        /// <summary>
        /// ClusterWithHash模式下获取实例
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public ZKReturn GetClusterNodeByHash(int val, out ClusterNodeInfo nodeInfo)
        {
            if (_ClusterMode != ClusterMode.ClusterWithHash)
            {
                nodeInfo = null;
                return ZKReturn.ZKReturnInvalid;
            }

            int valHash = val % GridDefines.CLUSTER_HASH_SIZE;

            string instanceId = string.Empty;
            _ClusterInstanceIdHashTable.TryGetValue(valHash, out instanceId);

            nodeInfo = GetClusterNodeByInstanceId(instanceId);
            return nodeInfo != null ? ZKReturn.ZKReturnSuccess : ZKReturn.ZKReturnFail;
        }

        /// <summary>
        /// 指定instanceId（zookeeper下的节点id），获取实例
        /// </summary>
        /// <param name="instanceId"></param>
        /// <returns></returns>
        public ClusterNodeInfo GetClusterNodeByInstanceId(string instanceId)
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

        
    }
}
