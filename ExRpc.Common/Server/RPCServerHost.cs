using System;
using System.Reflection;
using System.Collections.Specialized;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEZ.Core;
using NetEZ.Core.Client;
using NetEZ.Core.Server;
using NetEZ.Core.Protocol;
using NetEZ.Core.Protocol.JMP;
using NetEZ.Core.Event;
using ExRpc.Common.Protocol;

namespace ExRpc.Common.Server
{
    /// <summary>
    /// rpcserver容器,负责处理基本的socket通信;
    /// </summary>
    public class RPCServerHost : TcpServiceBase
    {
        /// <summary>
        /// server登记表
        /// </summary>
        private ConcurrentDictionary<int, IServer> _ServerTable = new ConcurrentDictionary<int, IServer>();
        private List<Assembly> _ServantAsmbList = new List<System.Reflection.Assembly>();
        //public ServerHostConfigure _ServerHostConfig = new ServerHostConfigure();

        //protected Dictionary<string, GNode> _Grids = new Dictionary<string, GNode>();
        protected ConcurrentDictionary<string, GridUri> _ServerGridUriTable = new ConcurrentDictionary<string, GridUri>();

        public List<Assembly> ServantAsmbs { get { return _ServantAsmbList; } }


        /// <summary>
        /// 加载servant库
        /// </summary>
        private void LoadServantAssembly()
        {
            string servantAssembly = _Config.GetItemValue("Assembly", "Servant");
            if (string.IsNullOrEmpty(servantAssembly))
                return;

            string[] asmbs = servantAssembly.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (asmbs == null || asmbs.Length < 1)
                return;

            foreach (string asmb in asmbs)
            {
                try
                {
                    _ServantAsmbList.Add(System.Reflection.Assembly.Load(asmb));
                }
                catch { }
            }
        }

        private void LoadServerGridTable()
        {
            List<KeyValuePair<string, string>> gridList = _Config.GetSection("Grid");
            if (gridList != null && gridList.Count > 0)
            {
                foreach (KeyValuePair<string, string> kvp in gridList)
                {
                    GridUri uri = GridUri.ParseFromString(kvp.Value);
                    if (uri == null)
                        continue;

                    _ServerGridUriTable.AddOrUpdate(kvp.Key.Trim().ToLower(), uri, (k, v) => uri);
                }
            }
        }

        //private void LoadGrids()
        //{
        //    List<KeyValuePair<string,string>> gridList = _Config.GetSection("Grid");

        //    if (gridList != null && gridList.Count > 0)
        //    {
        //        foreach (KeyValuePair<string, string> kvp in gridList)
        //        {
        //            GridUri gridUri = GridUri.ParseFromString(kvp.Value);
        //            if (gridUri == null)
        //                continue;


        //            if (!_Grids.ContainsKey(gridUri.Root))
        //            {
        //                GNode rootNode = new GNode(gridUri.Root);
        //                GNode projNode = new GNode(gridUri.Proj);
        //                GNode clusterNode = !string.IsNullOrEmpty(gridUri.Cluster) ? new GNode(gridUri.Cluster) : null;
        //                if (clusterNode != null)
        //                    projNode.AddChild(clusterNode);

        //                rootNode.AddChild(projNode);
        //            }
        //            else
        //            {
        //                GNode rootNode = _Grids[gridUri.Root];
        //                GNode projNode = rootNode.GetChild(gridUri.Proj);
        //                if (projNode == null)
        //                {
        //                    projNode = new GNode(gridUri.Proj);
        //                    GNode clusterNode = !string.IsNullOrEmpty(gridUri.Cluster) ? new GNode(gridUri.Cluster) : null;
        //                    if (clusterNode != null)
        //                        projNode.AddChild(clusterNode);

        //                    rootNode.AddChild(projNode);
        //                }
        //                else
        //                {
        //                    GNode clusterNode = !string.IsNullOrEmpty(gridUri.Cluster) ? new GNode(gridUri.Cluster) : null;
        //                    if (clusterNode != null)
        //                        projNode.AddChild(clusterNode);
        //                }
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// 加载rpc协议库
        /// </summary>
        private void LoadRpcAssembly()
        {
            //  添加协议signal前缀
            JMPParser.AppendIgnoredSignalPrefix(RpcCodeDefines.RPCCALL_PREFIX_REQUEST);
            JMPParser.AppendIgnoredSignalPrefix(RpcCodeDefines.RPCCALL_PREFIX_REQUEST);

            List<string> asmbList = new List<string>();

            //asmbList.Add("ExRpc.Common");

            string rpcAssembly = _Config.GetItemValue("Assembly", "Rpc");

            if (!string.IsNullOrEmpty(rpcAssembly))
            {
                string[] asmbs = rpcAssembly.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (asmbs != null && asmbs.Length > 0)
                {
                    foreach (string asmb in asmbs)
                    {
                        if (string.Compare(asmb, "ExRpc.Common", true) == 0)
                        {
                            continue;
                        }
                        asmbList.Add(asmb);
                    }
                }
            }

            NetEZ.Core.Protocol.JMP.JMPParser parser = new NetEZ.Core.Protocol.JMP.JMPParser(asmbList, _Logger);
            RegisterMessageParser(parser);
        }

        private bool InitServerHost()
        {
            //  加载servant库
            LoadServantAssembly();

            //  加载rpc协议库
            LoadRpcAssembly();

            //  尝试加载grid配置
            LoadServerGridTable();

            //  逐一启动server
            if (!InitServers())
                return false;

            return true;
        }

        private bool InitServers()
        {
            string serverAssembly = _Config.GetItemValue("Assembly", "Server");
            List<Assembly> asmbList = new List<Assembly>();

            if (!string.IsNullOrEmpty(serverAssembly))
            {
                string[] asmbs = serverAssembly.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (asmbs != null && asmbs.Length > 0)
                {
                    foreach (string asmb in asmbs)
                    {
                        asmbList.Add(System.Reflection.Assembly.Load(asmb));
                    }
                }
            }

            List<KeyValuePair<string, string>> servers = _Config.GetSection("Servers");
            if (servers == null || servers.Count < 1)
                return false;

            //  允许创建多个端口侦听实例
            foreach (KeyValuePair<string, string> kv in servers)
            {
                string servName = kv.Key;
                string hostPort = kv.Value;

                if (string.IsNullOrEmpty(hostPort))
                    continue;

                IServer server = null;
                HostInfo hostInfo = new HostInfo(hostPort);

                bool start = false;
                //  遍历每个库
                foreach (Assembly asmb in asmbList)
                {
                    //  遍历每个type
                    foreach (Type tp in asmb.ExportedTypes)
                    {
                        if (tp.IsAbstract)
                            continue;

                        if (
                            tp.Name.Equals(servName, StringComparison.CurrentCultureIgnoreCase)
                            && tp.GetInterface("ExRpc.Common.Server.IServer", false) != null

                            )
                        {
                            server = (IServer)Activator.CreateInstance(tp);
                            server.SetLogger(_Logger);
                            server.RegisterServerHost(this);
                            BindServer(hostInfo, server);
                            if (!server.Start())
                                return false;                   //  所有的server都要start成功
                            else
                            {
                                start = true;
                                break;
                            }
                        }
                    }

                    if (start)
                        break;
                }
            }
            return true;
        }

        public override bool Start()
        {
            if (!LoadConfigure())
                return false;

            if (!InitLogger())
                return false;

            if (!InitServerHost())
                return false;

            if (!base.Start())
                return false;

            return true;
        }

        public bool BindServer(HostInfo host, IServer server)
        {
            if (server == null)
                return false;

            int idx = -1;
            for (int i = 0; i < _Config.ListenHosts.Count; i++)
            {
                HostInfo hostItem = _Config.ListenHosts[i];
                if (hostItem.Host.Equals(host.Host, StringComparison.CurrentCultureIgnoreCase) && hostItem.Port == host.Port)
                {
                    idx = i;
                    server.SetHostInfo(host);
                    break;
                }
            }

            if (idx >= 0)
            {
                _ServerTable.AddOrUpdate(idx, server, (k, v) => server);
                return true;
            }

            return false;
        }

        public GridUri GetServerGridUri(string servName)
        {
            if (string.IsNullOrEmpty(servName))
                return null;

            GridUri uri = null;
            if (_ServerGridUriTable.TryGetValue(servName.ToLower(), out uri))
                return uri;

            return null;
        }

        protected override void OnClientReceivedMessage(TcpClientManager clmngr, NetEZ.Core.Protocol.IMessage msg)
        {
            IRpcCallMethod rpcReq = (IRpcCallMethod)msg;
            IServer server = null;
            string signal = rpcReq.GetSignal();

            if (signal.StartsWith(RpcCodeDefines.RPCCALL_PREFIX_REQUEST, StringComparison.CurrentCultureIgnoreCase))
            {
                //  RPC 调用
                if (_ServerTable.TryGetValue(clmngr.HostId, out server))
                {
                    bool ret = server.CallServantMethod(rpcReq, clmngr);
                    if (ret)
                    {
                        //  do somethine
                    }
                    else
                    {
                        LogError("OnClientReceivedMessage failed. Unknow signal.");
                    }
                }
            }
            else if (string.Compare(signal, "Ping", true) == 0)
            {

            }

            base.OnClientReceivedMessage(clmngr, msg);
        }
    }

}
