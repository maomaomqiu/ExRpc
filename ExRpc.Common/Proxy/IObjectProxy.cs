using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExRpc.Common.Server;
using NetEZ.Core.Server;

namespace ExRpc.Common.Proxy
{
    public interface IObjectProxy
    {
        string GetObjectName();
        void SetObjectName(string objName);

        string GetPhysicalObjectName();
        void SetHost(HostInfo host);

    }
}
