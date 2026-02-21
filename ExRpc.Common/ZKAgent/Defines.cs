using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExRpc.ZKAgent
{
    public class Defines
    {
        public const int SUCCESS = 0;
        
        public const int ZK_ERR_BASE = 51000;
        public const int ZK_ERR_INVALID_PARAM = ZK_ERR_BASE + 1;
        public const int ZK_ERR_CONNECT_FAILED_WITHRETRY = ZK_ERR_BASE + 2;
        public const int ZK_ERR_READ_FAILED = ZK_ERR_BASE + 3;
    }
}
