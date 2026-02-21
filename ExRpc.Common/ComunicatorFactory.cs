using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEZ.Utility.Logger;

namespace ExRpc.Common
{
    public class ComunicatorFactory
    {
        private volatile static Logger _Logger = null;
        private static object _Lock = new object();
        private volatile static NetEZ.Utility.Configure.SimpleXmlConfigure _CommConfig = null;
        private volatile static NetEZ.Utility.Configure.SimpleXmlConfigure _ZKConfig = null;
        private volatile static ConcurrentDictionary<string, Communicator> _CommunicatorTable = new ConcurrentDictionary<string, Communicator>();

        private static NetEZ.Utility.Configure.SimpleXmlConfigure GetCommConfig()
        {
            if (_CommConfig != null)
                return _CommConfig;

            try
            {
                lock (_Lock)
                {
                    if (_CommConfig != null)
                        return _CommConfig;

                    string filePath = "c:\\xconfig2\\communitor.xml";
                    _CommConfig = new NetEZ.Utility.Configure.SimpleXmlConfigure(filePath);
                }
            }
            catch { }

            return _CommConfig;
        }

        private static NetEZ.Utility.Configure.SimpleXmlConfigure GetZKConfig()
        {
            if (_ZKConfig != null)
                return _ZKConfig;

            try
            {
                lock (_Lock)
                {
                    if (_ZKConfig != null)
                        return _ZKConfig;

                    string filePath = "c:\\xconfig2\\zookeeper.xml";
                    _ZKConfig = new NetEZ.Utility.Configure.SimpleXmlConfigure(filePath);
                }
            }
            catch { }

            return _ZKConfig;
        }

        private static Logger GetLogger()
        {
            if (_Logger != null)
                return _Logger;

            try
            {
                lock (_Lock)
                {
                    if (_Logger != null)
                        return _Logger;

                    NetEZ.Utility.Configure.SimpleXmlConfigure config = GetCommConfig();
                    if (config == null)
                        return null;

                    string logPath = config.GetItemValue("log", "path");
                    string templateName = config.GetItemValue("log", "template");
                    int logLevel = 0;
                    if (!config.GetItemInt32Value("log", "level", 7, out logLevel))
                        logLevel = 7;

                    if (string.IsNullOrEmpty(logPath) || string.IsNullOrEmpty(templateName))
                        return null;

                    _Logger = new Logger(logPath, templateName);

                    if ((logLevel & 1) > 0)
                        _Logger.EnableLogLevel(LogLevel.Info);
                    if ((logLevel & 2) > 0)
                        _Logger.EnableLogLevel(LogLevel.Debug);
                    if ((logLevel & 4) > 0)
                        _Logger.EnableLogLevel(LogLevel.Error);

                    return _Logger;
                }
            }
            catch { }

            return null;
        }

        public static Communicator GetCommunicator(GridUri gridUri, string host)
        {
            if (gridUri == null || string.IsNullOrEmpty(gridUri.Root))
                return null;

            NetEZ.Utility.Configure.SimpleXmlConfigure config = GetZKConfig();
            if (config == null)
                return null;

            string cacheKey = string.Format("{0}.{1}", host.ToLower(), gridUri.ToString());
            if (string.IsNullOrEmpty(cacheKey))
                return null;

            try
            {
                string zkHost = config.GetItemValue(gridUri.Root, 0);
                if (string.IsNullOrEmpty(zkHost))
                    return null;


            }
            catch { }
            finally { }

            return null;
        }

        /// <summary>
        /// 获取指定名字的Communicator
        /// </summary>
        /// <param name="cfgName"></param>
        /// <returns></returns>
        public static Communicator GetCommunicator(string cfgName)
        {
            if (cfgName != null)
                cfgName = cfgName.Trim().ToLower();
            if (string.IsNullOrEmpty(cfgName))
                return null;


            Communicator comm = null;
            if (_CommunicatorTable.TryGetValue(cfgName, out comm))
                return comm;

            NetEZ.Utility.Configure.SimpleXmlConfigure config = GetCommConfig();
            if (config == null)
                return null;

            try
            {
                //int port = 0;
                List<NetEZ.Core.Server.HostInfo> hostList = new List<NetEZ.Core.Server.HostInfo>();
                int connTimeout = CommunicatorConfigure.CONNECT_TIMEOUT_MS;
                int requestTimeout = CommunicatorConfigure.REQUEST_TIMEOUT_SEND_MS;
                int ackTimeout = CommunicatorConfigure.REQUEST_TIMEOUT_WAITING_ACK_MS;
                int retryTimes = CommunicatorConfigure.RETRY_TIMES;
                int poolSize = CommunicatorConfigure.CONNECTION_POOL_SIZE_DEFAULT;

                string hostRaw = string.Empty;
                List<string> asmbList = new List<string>();

                hostRaw = config.GetItemValue(cfgName, "Host");
                if (!string.IsNullOrEmpty(hostRaw))
                {
                    string[] hostsBuf = hostRaw.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (hostsBuf != null && hostsBuf.Length > 0)
                    {
                        for (int i = 0; i < hostsBuf.Length; i++)
                        {
                            hostList.Add(NetEZ.Core.Server.HostInfo.ParseFromString(hostsBuf[i]));
                        }
                    }
                }

                //  host必须提供
                if (hostList.Count < 1)
                    return null;

                //config.GetItemInt32Value(name, "Port", 0, out port);

                config.GetItemInt32Value(cfgName, "RequestTimeout", CommunicatorConfigure.REQUEST_TIMEOUT_SEND_MS, out requestTimeout);
                config.GetItemInt32Value(cfgName, "AckTimeout", CommunicatorConfigure.REQUEST_TIMEOUT_WAITING_ACK_MS, out ackTimeout);
                config.GetItemInt32Value(cfgName, "RetryTimes", CommunicatorConfigure.RETRY_TIMES, out retryTimes);
                config.GetItemInt32Value(cfgName, "PoolSize", CommunicatorConfigure.CONNECTION_POOL_SIZE_DEFAULT, out poolSize);
                config.GetItemInt32Value(cfgName, "ConnectTimeout", CommunicatorConfigure.CONNECT_TIMEOUT_MS, out connTimeout);

                CommunicatorConfigure commConfig = new CommunicatorConfigure();
                commConfig.RequestTimeout = requestTimeout;
                commConfig.RequestWaitingAckTimeout = ackTimeout;
                commConfig.RetryTimes = retryTimes;
                commConfig.ConnectionPoolSize = poolSize;
                commConfig.ConnectTimeout = connTimeout;

                string asmbString = config.GetItemValue(cfgName, "Assembly");
                if (!string.IsNullOrEmpty(asmbString))
                {
                    string[] asmbBuf = asmbString.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (asmbBuf != null && asmbBuf.Length > 0)
                    {
                        foreach (string asmb in asmbBuf)
                        {
                            asmbList.Add(asmb);
                        }
                    }
                }

                comm = new Communicator(GetLogger(), commConfig);
                comm.LoadAssemby(asmbList);
                comm.Hosts = hostList;
                if (comm.Start())
                {
                    _CommunicatorTable.AddOrUpdate(cfgName, comm, (k, v) => v);

                    return comm;
                }
            }
            catch { }

            return null;
        }
    }
}
