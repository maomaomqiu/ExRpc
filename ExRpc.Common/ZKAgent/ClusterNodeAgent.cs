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

namespace ExRpc.ZKAgent
{
    public class ClusterNodeAgent : ZKClient, IDisposable
    {
        const string PATH_PREFIX_ROOT = "/proj-cpc/{0}/{1}/register";
        const string PATH_PREFIX_NODE = "/proj-cpc/{0}/{1}/register/s";

        protected string _PathRoot = string.Empty;
        protected string _PathNodePrefix = string.Empty;
        protected ClusterNodeInfo _NodeInfo = null;
        protected string _ZKNodeInstanceId = string.Empty;
        protected List<ClusterNodeInfo> _ClusterNodes = null;

        protected event OnClusterNodeChanged _OnClusterNodeChangedHanlder = null;
        protected ChildrenChangedWatcher _OnClusterNodesChangedWatcher = null;

        public ClusterNodeAgent(ClusterNodeInfo nodeInfo, ZKConnectInfo zkConn = null, Logger logger = null)
        {
            SetLogger(logger);

            if (nodeInfo == null)
                throw new Exception("ClusterNodeInfo must be supplied.");

            _NodeInfo = nodeInfo;

            _PathRoot = string.Format(PATH_PREFIX_ROOT, _NodeInfo.ProjName, _NodeInfo.ClusterName);
            _PathNodePrefix = string.Format(PATH_PREFIX_NODE, _NodeInfo.ProjName, _NodeInfo.ClusterName);
        }

        ~ClusterNodeAgent()
        {
            Dispose();
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

            //  连接成功后要做的第一件事情就是登记节点
            RegisterClusterNode();

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
                    string zkHosts = config.GetItemValue(_NodeInfo.RootName, 0);
                    zkConn = new ZKConnectInfo(zkHosts);
                }

                if (!InitZKAgent(zkConn))
                {
                    LogError(string.Format("[ClusterNodeAgent] starting failed(InitZKAgent return fail). proj={0}; cluster={1}; node={2}", _NodeInfo.ProjName, _NodeInfo.ClusterName, _NodeInfo.NodeName));
                    return false;
                }

                //  同步模式连接到zk服务器
                ZKReturn zkRet = ConnectToZKServer(true);
                if (!zkRet.IsSuccess)
                {
                    LogError(string.Format("[ClusterNodeAgent] starting failed. ConnectToZKServer return={0}-{1}; ", zkRet.Code, zkRet.Msg));
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

        public ZKReturn GetClusterNodes()
        {
            List<ClusterNodeInfo> nodes = null;
            byte[] buf = null;
            List<string> nodeIdList = null;

            lock (this)
            {
                if (_OnClusterNodesChangedWatcher == null)
                    _OnClusterNodesChangedWatcher = new ChildrenChangedWatcher(OnClusterNodesChangedEventHandler);

                
                //ZKReturn ret = ListChildren(_PathRoot, _OnClusterNodesChangedWatcher, out nodeIdList);
                nodeIdList = ListChildren(_PathRoot, _OnClusterNodesChangedWatcher).Result;
                //if (!ret.IsSuccess)
                //{
                //    LogError(string.Format("[GetClusterNodes] failed. ret={0}; path={1}", ret.Code, _PathRoot));
                //    return ret;
                //}

                //if (nodeIdList == null || nodeIdList.Count < 1)
                //{
                //    //  不应该出现的结果,但此时必须返回成功
                //    LogError(string.Format("[GetClusterNodes] nodes list is empty. path={0}", _PathRoot));
                //    return ZKReturn.ZKReturnFail;
                //}

                if (nodeIdList != null && nodeIdList.Count > 0)
                {
                    foreach (string nodeId in nodeIdList)
                    {
                        //  逐一读取每个节点数据
                        string path = string.Format("{0}/{1}", _PathRoot, nodeId);
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
                            if (string.Compare(nodeId, _ZKNodeInstanceId, true) == 0)
                                nodeInfo.SetOwner();

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
                    _ClusterNodes = nodes.OrderBy(c => c.InstanceNodeId).ToList();
                }

                //Console.WriteLine("[GetClusterNodes] nodes={0}; #{1}#", nodes != null ? nodes.Count : 0, DateTime.Now.ToString("HH:mm:ss"));
            }

            if (_OnClusterNodeChangedHanlder != null)
                _OnClusterNodeChangedHanlder(_ClusterNodes);

            return ZKReturn.ZKReturnSuccess;
        }

        public ZKReturn RegisterClusterNode()
        {
            string nodeInstanceId = string.Empty;
            string nodeData = JsonConvert.SerializeObject(_NodeInfo);
            ZKReturn ret = ZKReturn.ZKReturnFail;
            while (!ret.IsSuccess)
            {
                ret = Create(_PathNodePrefix, nodeData, null, CreateMode.EPHEMERAL_SEQUENTIAL, out nodeInstanceId);

                if (!ret.IsSuccess)
                {
                    _ZKNodeInstanceId = string.Empty;
                    LogError(string.Format("[ClusterNodeAgent] RegisterClusterNode return={0}-{1}; ", ret.Code, ret.Msg));
                    //  注册失败一般是无法连接到zk服务器，需要不断重试
                    Thread.Sleep(500);
                }
                else
                {
                    _ZKNodeInstanceId = nodeInstanceId;
                }
            }

            return ret;
        }
    }
}
