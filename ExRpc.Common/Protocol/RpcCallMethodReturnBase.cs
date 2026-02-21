using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEZ.Core.Protocol;
using NetEZ.Core.Protocol.JMP;

namespace ExRpc.Common.Protocol
{
    public class RpcCallMethodReturnBase : JMPMessaeBase, IRpcCallMethodReturn
    {
        protected bool _IsError = false;

        public long __cb_id { get; set; }
        public long __tid { get; set; }
        public bool __err { get { return _IsError; } set { _IsError = value; } }

        protected RpcCallMethodReturnBase()
        {
            //_Signal = "__rpccallret:RpcCallMethodReturn";
            MakeSignal();
        }

        protected void MakeSignal()
        {
            _Signal = string.Format("{0}{1}", RpcCodeDefines.RPCCALL_PREFIX_RESPONSE, this.GetType().Name);
        }
    }

    public class RpcCallMethodReturnInvalid : RpcCallMethodReturnBase
    {
        public RpcCallMethodReturnInvalid() : base() { _IsError = true; }
    }
}
