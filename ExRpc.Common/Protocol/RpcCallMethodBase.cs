using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEZ.Core.Protocol;
using NetEZ.Core.Protocol.JMP;

namespace ExRpc.Common.Protocol
{
    public class RpcCallMethodBase : JMPMessaeBase,IRpcCallMethod
    {
        public long __cb_id { get; set; }
        public long __tid { get; set; }
        
        public string __servant { get; set; }
        public string __method { get; set; }
        //public IServantMethodParam __param { get; set; }

        protected RpcCallMethodBase()
        {
            MakeSignal();
            //_Signal = "__rpccall:RpcCallMethodBase";
        }

        protected void MakeSignal()
        {
            _Signal = string.Format("{0}{1}", RpcCodeDefines.RPCCALL_PREFIX_REQUEST, this.GetType().Name);
        }
    }
}
