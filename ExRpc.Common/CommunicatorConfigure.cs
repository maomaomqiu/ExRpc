using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExRpc.Common
{
    public class CommunicatorConfigure
    {
        public const int CONNECTION_POOL_SIZE_DEFAULT = 50;
        public const int CONNECT_TIMEOUT_MS = 200;
        public const int REQUEST_TIMEOUT_SEND_MS = 3000;
        public const int REQUEST_TIMEOUT_WAITING_ACK_MS = 9000;
        public const int RETRY_TIMES = 2;

        public int ConnectionPoolSize = CONNECTION_POOL_SIZE_DEFAULT;
        public int ConnectTimeout = CONNECT_TIMEOUT_MS;
        public int RequestTimeout = REQUEST_TIMEOUT_SEND_MS;
        public int RequestWaitingAckTimeout = REQUEST_TIMEOUT_WAITING_ACK_MS;
        public int RetryTimes = RETRY_TIMES;
        public string Assembly = string.Empty;
    }
}
