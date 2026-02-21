using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEZ.Core.Client;
using NetEZ.Core.Server;
using ExRpc.Common.Protocol;

namespace ExRpc.Common.Server
{
    public interface IServer
    {
        string GetServerName();

        NetEZ.Utility.Logger.Logger Logger { get; }
        void RegisterServerHost(RPCServerHost svrHost);
        void SetLogger(NetEZ.Utility.Logger.Logger logger);
        void SetHostInfo(HostInfo host);
        bool Start();
        void Stop(int timeout);
        bool CallServantMethod(IRpcCallMethod callReq, TcpClientManager clmngr);
    }
}
