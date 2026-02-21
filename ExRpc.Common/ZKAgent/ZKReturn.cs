using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExRpc.ZKAgent
{
    public class ZKReturn
    {
        public static ZKReturn ZKReturnSuccess = new ZKReturn(Defines.SUCCESS, "");
        public static ZKReturn ZKReturnFail = new ZKReturn(Defines.ZK_ERR_BASE, "失败.");
        public static ZKReturn ZKReturnInvalid = new ZKReturn(Defines.ZK_ERR_INVALID_PARAM, "无效的参数.");
        public static ZKReturn ZKReturnNoConnection = new ZKReturn(Defines.ZK_ERR_CONNECT_FAILED_WITHRETRY, "通信失败.");

        public int Code;
        public string Msg;

        public bool IsSuccess { get { return Code == Defines.SUCCESS ? true : false; } }

        public ZKReturn() { }
        public ZKReturn(int code, string msg)
        {
            Code = code;
            Msg = msg != null ? msg : string.Empty;
        }
    }
}
