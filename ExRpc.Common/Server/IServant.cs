using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExRpc.Common.Protocol;

namespace ExRpc.Common.Server
{
    public interface IServant
    {
        string GetServantName();
        void SetLogger(NetEZ.Utility.Logger.Logger logger);
        bool call_method(IRpcCallMethod request,out IRpcCallMethodReturn response);
    }
}
