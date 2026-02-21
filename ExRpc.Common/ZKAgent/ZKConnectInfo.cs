using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExRpc.ZKAgent
{
    public class ZKConnectInfo
    {
        /// <summary>
        /// 默认TTL为1个小时,单位:秒
        /// </summary>
        public const int DEFAULT_TTL = 60 * 60;

        /// <summary>
        /// 默认连接超时，单位ms
        /// </summary>
        public const int DEFAULT_CONNECTION_TIMEOUT = 4000;

        /// <summary>
        /// 默认连接重试次数
        /// </summary>
        public const int DEFAULT_CONNECTION_RETRYTIMES = 3;

        /// <summary>
        /// 连接串
        /// </summary>
        public string ConnectString = string.Empty;

        /// <summary>
        /// 连接超时时间，单位ms
        /// </summary>
        public int ConnectTimeout = DEFAULT_CONNECTION_TIMEOUT;

        /// <summary>
        /// 连接失败重试次数
        /// </summary>
        public int ConnectRetryTimes = DEFAULT_CONNECTION_RETRYTIMES;

        /// <summary>
        /// TTL，单位:秒
        /// </summary>
        public int TTL = DEFAULT_TTL;

        public ZKConnectInfo() { }
        public ZKConnectInfo(string conn) { ConnectString = conn; }
    }
}
