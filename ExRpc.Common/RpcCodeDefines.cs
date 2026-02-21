using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExRpc.Common
{
    public class RpcCodeDefines
    {
        public const string RPCCALL_PREFIX_REQUEST = "__rpccall:";
        public const string RPCCALL_PREFIX_RESPONSE = "__rpccallret:";

        public const int SUCCESS = 0;

        public const short TRAN_STATUS_RESET = 0;
        public const short TRAN_STATUS_SEND_FAIL = 500;
        public const short TRAN_STATUS_OK = 200;

        public const int RPC_ERR_COMMUNICATOR_BASE = 1000;
        public const int RPC_ERR_COMMUNICATOR_NO_CLIENT = RPC_ERR_COMMUNICATOR_BASE + 1;
        public const int RPC_ERR_COMMUNICATOR_CLIENT_CLOSED = RPC_ERR_COMMUNICATOR_BASE + 2;
        public const int RPC_ERR_COMMUNICATOR_OBTAIN_FAILED = RPC_ERR_COMMUNICATOR_BASE + 3;
        public const int RPC_ERR_COMMUNICATOR_HOSTS_INVALID = RPC_ERR_COMMUNICATOR_BASE + 4;

        public const int RPC_ERR_REQ_BASE = 2000;
        public const int RPC_ERR_REQ_SEND_FAIL = RPC_ERR_REQ_BASE + 1;
        public const int RPC_ERR_REQ_SEND_FAIL_RETRY = RPC_ERR_REQ_BASE + 2;

        public const int RPC_ERR_ACK_BASE = 3000;
        public const int RPC_ERR_ACK_SERVER_NO_RESPOND = RPC_ERR_ACK_BASE + 1;
    }
}
