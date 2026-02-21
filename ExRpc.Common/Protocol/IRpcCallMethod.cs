using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEZ.Core.Protocol;
using NetEZ.Core.Protocol.JMP;

namespace ExRpc.Common.Protocol
{
    public interface IRpcCallMethod : IJMPMessage
    {
        long __cb_id { get; set; }
        long __tid { get; set; }

        string __servant { get; set; }
        string __method { get; set; }
    }

    public interface IRpcCallMethodReturn:IJMPMessage
    {
        bool __err { get; set; }
        long __cb_id { get; set; }
        long __tid { get; set; }
    }
}
