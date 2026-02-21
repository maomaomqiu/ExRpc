using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEZ.Core;
using NetEZ.Core.Client;
using NetEZ.Core.Server;
using NetEZ.Core.Protocol;
using NetEZ.Core.Protocol.JMP;
using NetEZ.Utility.Logger;
using ExRpc.Common.Proxy;
using ExRpc.Common.Server;
using ExRpc.Common.Protocol;

namespace ExRpc.Common
{
    public class Communicator:LoggerBO
    {
        private ConcurrentDictionary<string, TcpClientPool> _ConnectionPoolTable = new ConcurrentDictionary<string, TcpClientPool>();
        private CommunicatorConfigure _Configure = null;
        //private ConcurrentQueue<Transaction> _TransactionQueue = new ConcurrentQueue<Transaction>();
        private ConcurrentDictionary<long, Transaction> _TransactionTable = new ConcurrentDictionary<long, Transaction>();
        private JMPParser _MessageParser = null;
        private List<string> _AssemblyList = null;
        private List<HostInfo> _Hosts = null;
        private volatile bool _IsCleanTranThreadRunning = false;
        private Thread _CleanTranThread = null;
        private string _ModuleName = string.Empty;

        public CommunicatorConfigure Config { get { return _Configure; } }
        public List<HostInfo> Hosts { get { return _Hosts; } set { _Hosts = value; } }

        public Communicator() { _Configure = new CommunicatorConfigure(); }
        public Communicator(Logger logger, CommunicatorConfigure config) 
        { 
            SetLogger(logger);
            _Configure = config != null ? config : new CommunicatorConfigure();
        }
        public Communicator(string moduleName, Logger logger, CommunicatorConfigure config)
        {
            if (moduleName != null)
                _ModuleName = moduleName.ToLower();
            SetLogger(logger);
            _Configure = config != null ? config : new CommunicatorConfigure();
        }

        public IObjectProxy CreateObjectProxy(HostInfo host, string objectName)
        {
            return null;
        }

        private void InitCleanTranThread()
        {
            _IsCleanTranThreadRunning = true;
            _CleanTranThread = new Thread(CleaningExpiredTransactions);
            _CleanTranThread.IsBackground = true;
            _CleanTranThread.Start();
            while (!_CleanTranThread.IsAlive)
                Thread.Sleep(3);
        }

        private void CleaningExpiredTransactions(object state)
        {
            List<long> tranIdBuf = new List<long>(1024);
            Transaction tranTmp = null;
            while (_IsCleanTranThreadRunning)
            {
                Thread.Sleep(5000);
                int cnt = 0;

                try
                {
                    foreach (long tranId in _TransactionTable.Keys)
                    {
                        if (cnt < 1024)
                        {
                            tranIdBuf.Add(tranId);
                            cnt++;
                        }
                        else
                            break;
                    }

                    if (cnt == 0)
                        continue;

                    foreach (long tranId in tranIdBuf)
                    {
                        if (!_TransactionTable.TryGetValue(tranId, out tranTmp))
                            continue;

                        if (tranTmp.expire_time.AddSeconds(60) < DateTime.Now)
                        { 
                            //  已经失效，因为服务器未返回消息而滞留内存
                            _TransactionTable.TryRemove(tranId, out tranTmp);
                        }
                    }
                }
                catch { }
                finally
                {
                    if (tranIdBuf.Count > 0)
                        tranIdBuf.Clear();
                }
            }
        }

        public void CloseClient(HostInfo host, TcpClientBase client)
        {
            if (host == null)
                return;

            TcpClientPool pool = GetConnectionPool(host);
            if (pool == null)
            {
                LogError(string.Format("[CloseClient] GetConnectionPool failed."));
                return;
            }
            pool.ReleaseClient(client);
            
        }

        public int SendMessage(HostInfo host, IRpcCallMethod request, Transaction tran)
        {
            TcpClientPool pool = GetConnectionPool(host);
            if (pool == null)
                return RpcCodeDefines.RPC_ERR_COMMUNICATOR_NO_CLIENT;

            TcpClientBase client = pool.GetClient(_Configure.ConnectTimeout);
            if (client == null)
            {
                LogError(string.Format("[SendMessage] GetClient failed."));
                return RpcCodeDefines.RPC_ERR_COMMUNICATOR_NO_CLIENT;
            }
                

            //  Transaction mode
            //      bit0 : 0 = 表示无视发送结果，即send后不管，也不会retry; 1 = 关注发送结果，并视配置里的retry值进行多次尝试
            //      bit1 : 1 = 需要等待服务器响应
            
            request.__cb_id = tran.__cb_id;
            tran.client = client;           //  通信完成后要回收

            //  登记transaction
            if (!tran.registered)
                RegisterTransaction(tran);

            int bytes = 0;
            byte[] buf = request.ToBytes(out bytes);

            if (client.SendBytesAsync(buf, 0, bytes, tran))
                return RpcCodeDefines.SUCCESS;
            else
                return RpcCodeDefines.RPC_ERR_COMMUNICATOR_CLIENT_CLOSED;
        }

        private void RegisterTransaction(Transaction tran)
        {
            if (tran == null)
                return;

            //  只有需要等待返回结果的事务才登记
            if ((tran.mode & Transaction.MODE_SEND_WAITING_ACK) > 0)
            {
                _TransactionTable.AddOrUpdate(tran.__cb_id, tran, (k, v) => tran);
                tran.registered = true;
            }
        }

        public void UnRegisterTransaction(long cbId)
        {
            Transaction tran = null;
            _TransactionTable.TryRemove(cbId, out tran);

            try { tran.Destroy(); }
            catch { }
            finally { tran = null; }
        }

        public void LoadAssemby(List<string> assemblyList)
        {
            _AssemblyList = assemblyList;
        }

        public bool Start()
        {
            if (_AssemblyList == null || _AssemblyList.Count < 1)
                return false;

            _AssemblyList.Add("ExRpc.Common");

            JMPParser.AppendIgnoredSignalPrefix(RpcCodeDefines.RPCCALL_PREFIX_REQUEST);
            JMPParser.AppendIgnoredSignalPrefix(RpcCodeDefines.RPCCALL_PREFIX_RESPONSE);

            _MessageParser = new JMPParser(_ModuleName, _AssemblyList, _Logger);

            InitCleanTranThread();

            return true;
        }

        private TcpClientPool GetConnectionPool(HostInfo host)
        { 
            TcpClientPool pool = null;
            if (_ConnectionPoolTable.TryGetValue(host.ToString(),out pool))
                return pool;

            pool = new TcpClientPool(_Configure.ConnectionPoolSize, _Logger);
            pool.OnRecvServerDataCallback += OnReceivedServerData;
            pool.OnSendCompletedCallback += OnSendCompleted;
            if (!pool.Start(host.Host, host.Port))
                return null;

            _ConnectionPoolTable.AddOrUpdate(host.ToString(), pool, (k, v) => pool);

            return pool;
        }

        private void OnReceivedServerData(IClient client, byte[] rawMsgBytes)
        {
            IMessage msg = null;

            if (!_MessageParser.ParseMessageFromBytes(rawMsgBytes, out msg) || msg == null)
                return;

            IJMPMessage jmpMsg = (IJMPMessage)msg;
            string signal = jmpMsg.GetSignal();
            if (signal.StartsWith(RpcCodeDefines.RPCCALL_PREFIX_RESPONSE, StringComparison.CurrentCultureIgnoreCase))
            {
                IRpcCallMethodReturn rpcRet = (IRpcCallMethodReturn)msg;
                long cbId = rpcRet.__cb_id;
                Transaction tran = null;
                if (_TransactionTable.TryGetValue(cbId, out tran))
                {
                    tran.call_return = rpcRet;
                    if (tran.ack_evt != null)
                        tran.ack_evt.Set();
                }
            }
        }

        private void OnSendCompleted(IClient client, object state, int code)
        {

            Transaction tran = null;

            if (state != null)
            {
                tran = (Transaction)state;

                //  code == 0 表示发送成功
                if (code == RpcCodeDefines.SUCCESS)
                {
                    tran.status = RpcCodeDefines.TRAN_STATUS_OK;
                }
                else
                {
                    tran.status = RpcCodeDefines.TRAN_STATUS_SEND_FAIL;
                }

                if (tran.send_evt != null)
                    tran.send_evt.Set();
            }
        }
    }
}
