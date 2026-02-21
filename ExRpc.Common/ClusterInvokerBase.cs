using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEZ.Utility.Logger;
using ExRpc.Common.Proxy;
using ExRpc.Common;
using ExRpc.ZKAgent;
using ExRpc.ZKAgent.Event;
using ExRpc.ZKAgent.Watcher;

namespace ExRpc.Common
{
    public class ClusterInvokerBase:LoggerBO
    {
        private static object _Locker = new object();
        private static ConcurrentDictionary<string, ClusterClient> _ClusterClientTable = new ConcurrentDictionary<string, ClusterClient>();
        private static ConcurrentDictionary<string, Communicator> _CommunicatorTable = new ConcurrentDictionary<string, Communicator>();

        protected volatile ClusterClient _ClusterClient = null;
        protected volatile ClusterMode _ClusterMode = ClusterMode.None;

        /// <summary>
        /// 只能读取本对象，不能增、删、改！
        /// </summary>
        protected volatile List<ClusterNodeInfo> _ClusterNodes = null;
        
        protected GridUri _Uri = null;
        protected string _UriString = string.Empty;

        protected ClusterInvokerBase(ClusterMode mode, GridUri uri, Logger logger = null)
        {
            _ClusterMode = mode;
            _Uri = uri;
            if (_Uri == null || string.IsNullOrEmpty(_Uri.Root) || string.IsNullOrEmpty(_Uri.Proj) || string.IsNullOrEmpty(_Uri.Cluster))
                throw new Exception("grid is invalid.");

            _UriString = _Uri.ToString();
            SetLogger(logger);

            if (!InitClusterClient())
                throw new Exception("InitCluster failed.");
        }

        protected ClusterInvokerBase(GridUri uri, Logger logger = null)
        {
            _ClusterMode = ClusterMode.None;
            _Uri = uri;
            if (_Uri == null || string.IsNullOrEmpty(_Uri.Root) || string.IsNullOrEmpty(_Uri.Proj) || string.IsNullOrEmpty(_Uri.Cluster))
                throw new Exception("grid is invalid.");

            _UriString = _Uri.ToString();
            SetLogger(logger);
            if (!InitClusterClient())
                throw new Exception("InitCluster failed.");
        }

        protected virtual bool InitClusterClient()
        {
            if (GetClusterClient() == null)
                return false;
            return true;
        }

        private ClusterClient GetClusterClient()
        {
            if (_ClusterClient != null)
                return _ClusterClient;

            lock (_Locker)
            {
                if (_ClusterClient != null)
                    return _ClusterClient;

                ClusterClient client = null;
                if (_ClusterClientTable.TryGetValue(_UriString, out client))
                {
                    _ClusterClient = client;
                    return _ClusterClient;
                }

                try
                {
                    client = new ClusterClient(_Uri.Root, _Uri.Proj, _Uri.Cluster, _ClusterMode);
                    client.SetLogger(_Logger);
                    //  注册节点变更事件
                    client.RegisterNodeChangedHanlder(OnClusterNodeChangedHandler);

                    if (!client.Start())
                    {
                        LogError(string.Format("[GetClusterClient] failed: ZKAgent start failed({0}).", _UriString));
                        return null;
                    }

                    if (string.IsNullOrEmpty(client.CommunicatorConfig.Assembly))
                    {
                        LogError(string.Format("[GetClusterClient] Assembly must be supplied in communicator configure({0}).", _UriString));
                        return null;
                    }

                    _ClusterClient = client;
                    _ClusterClientTable.AddOrUpdate(_UriString, _ClusterClient, (k, v) => _ClusterClient);

                    return _ClusterClient;
                }
                catch (Exception ex)
                {
                    LogError(string.Format("[GetClusterClient] Exception: Uri={0}; Ex={1}-{2}", _UriString, ex.Message, ex.StackTrace));
                }
                finally { }

                return null;
            }
        }
        protected virtual void OnClusterNodeChangedHandler(List<ClusterNodeInfo> nodes)
        {
            _ClusterNodes = nodes;
        }

        protected Communicator GetCommunicator(ClusterNodeInfo nodeInfo)
        {
            if (nodeInfo == null || string.IsNullOrEmpty(nodeInfo.NodeName) || string.IsNullOrEmpty(nodeInfo.Host) || nodeInfo.Port < 1)
                return null;

            string key = string.Format("{0}:{1}", nodeInfo.Host, nodeInfo.Port);

            Communicator comm = null;
            if (_CommunicatorTable.TryGetValue(key, out comm))
                return comm;

            try
            {
                List<string> asmbList = new List<string>();
                List<NetEZ.Core.Server.HostInfo> hostList = new List<NetEZ.Core.Server.HostInfo>();
                
                hostList.Add(new NetEZ.Core.Server.HostInfo(nodeInfo.Host, nodeInfo.Port));
                string[] asmbBuf = _ClusterClient.CommunicatorConfig.Assembly.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (asmbBuf != null && asmbBuf.Length > 0)
                {
                    foreach (string asmb in asmbBuf)
                    {
                        asmbList.Add(asmb);
                    }
                }
                string commModuleName = string.Format("{0}.{1}.{2}", nodeInfo.ClusterName, nodeInfo.ProjName, nodeInfo.RootName);

                comm = new Communicator(commModuleName, _Logger, _ClusterClient.CommunicatorConfig);
                comm.LoadAssemby(asmbList);
                comm.Hosts = hostList;

                if (comm.Start())
                {
                    _CommunicatorTable.AddOrUpdate(key, comm, (k, v) => v);

                    return comm;
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format("[GetCommunicator] Exception: Ex={0}-{1}", ex.Message, ex.StackTrace));
            }
            finally { }

            return null;
        }

        /// <summary>
        /// 使用指定数值的求余值获取实例
        /// </summary>
        /// <param name="val"></param>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        protected ZKReturn GetClusterNodeByMod(long val, out ClusterNodeInfo nodeInfo)
        {
            return _ClusterClient.GetClusterNodeByMod(val, out nodeInfo);
        }

        /// <summary>
        /// 使用指定数值的求余值获取实例
        /// </summary>
        /// <param name="val"></param>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        protected ZKReturn GetClusterNodeByMod(int val, out ClusterNodeInfo nodeInfo)
        {
            return _ClusterClient.GetClusterNodeByMod(val, out nodeInfo);
        }

        /// <summary>
        /// 随机获取节点实例
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        protected ZKReturn GetClusterNodeByRandom(out ClusterNodeInfo nodeInfo)
        {
            return _ClusterClient.GetClusterNodeByRandom(out nodeInfo);
        }

        /// <summary>
        /// Master-Slave模式下（随机）获取Slave实例
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        protected ZKReturn GetSlaveClusterNode(out ClusterNodeInfo nodeInfo)
        {
            return _ClusterClient.GetSlaveClusterNode(out nodeInfo);
        }

        /// <summary>
        /// Master-Slave模式下获取主机
        /// </summary>
        /// <returns></returns>
        protected ZKReturn GetMasterClusterNode(out ClusterNodeInfo nodeInfo)
        {
            return _ClusterClient.GetMasterClusterNode(out nodeInfo);
        }

        /// <summary>
        /// 指定InstanceId获取Node实例
        /// </summary>
        /// <param name="nodeInstanceId"></param>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        protected ZKReturn GetClusterNodeById(string nodeInstanceId, out ClusterNodeInfo nodeInfo)
        {
            return _ClusterClient.GetClusterNodeById(nodeInstanceId, out nodeInfo);
        }

        /// <summary>
        /// ClusterWithHash模式下获取实例
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        protected ZKReturn GetClusterNodeByHash(long val, out ClusterNodeInfo nodeInfo)
        {
            return _ClusterClient.GetClusterNodeByHash(val, out nodeInfo);
        }

        /// <summary>
        /// ClusterWithHash模式下获取实例
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        protected ZKReturn GetClusterNodeByHash(int val, out ClusterNodeInfo nodeInfo)
        {
            return _ClusterClient.GetClusterNodeByHash(val, out nodeInfo);
        }

        /// <summary>
        /// 指定instanceId（zookeeper下的节点id），获取实例
        /// </summary>
        /// <param name="instanceId"></param>
        /// <returns></returns>
        protected ClusterNodeInfo GetClusterNodeByInstanceId(string instanceId)
        {
            return _ClusterClient.GetClusterNodeByInstanceId(instanceId);
        }
    }
}
