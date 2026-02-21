using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExRpc.Common
{
    public class GridDefines
    {
        public const string GRIDNAME_ROOT = "wxroot";
        public const int CLUSTER_HASH_SIZE = 1000;
    }

    public enum ClusterMode
    {
        None = 0,

        /// <summary>
        /// Master-Slave模式
        /// </summary>
        MasterSlave = 1,

        /// <summary>
        /// 通用Cluster模式
        /// </summary>
        Cluster = 2,

        /// <summary>
        /// 该模式下，会使用持久化的hash值访问相对固定的节点
        /// </summary>
        ClusterWithHash = 3,
    }
}
