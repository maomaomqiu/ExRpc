using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEZ.Core.Client;
using NetEZ.Core.Server;
using ExRpc.Common.Protocol;

namespace ExRpc.Common
{
    public class Transaction
    {
        public const short MODE_SEND_NONECONSISTENCE = 0;

        /// <summary>
        /// 确认发送结果
        /// </summary>
        public const short MODE_SEND_CONSISTENCE = 1;

        /// <summary>
        /// 需要等待服务器返回
        /// </summary>
        public const short MODE_SEND_WAITING_ACK = 2;

        private static long _CbIdSeed = 0;

        public long __tid;
        public long __cb_id;

        public TcpClientBase client;
        public DateTime create_time;
        public DateTime expire_time;

        public short status = RpcCodeDefines.TRAN_STATUS_RESET;
        public short send_retry = 0;
        public AutoResetEvent send_evt = null;
        public AutoResetEvent ack_evt = null;

        public short mode = 0;

        public bool registered = false;             //  在communicator中是否已登记
        public IRpcCallMethodReturn call_return = null;

        private static long GetCbId()
        {
            return Interlocked.Increment(ref _CbIdSeed);
        }

        public Transaction()
        {
            __cb_id = GetCbId();
            expire_time = DateTime.Now.AddMilliseconds(9000);                   //  默认过期时间
        }

        public Transaction(DateTime expiredTime)
        {
            __cb_id = GetCbId();
            expire_time = expiredTime;
        }

        public Transaction(int expiredMilSeconds)
        {
            __cb_id = GetCbId();
            expire_time = DateTime.Now.AddMilliseconds(expiredMilSeconds > 0 ? expiredMilSeconds : 9000);
        }

        public void SetModeConsistence(bool val)
        {
            if (val)
                mode =(short)(mode | MODE_SEND_CONSISTENCE);
            else
                mode = (short)(mode & 0xfe);
        }

        public void SetModeWaitingAck(bool val)
        {
            if (val)
                mode = (short)(mode | MODE_SEND_WAITING_ACK);
            else
                mode = (short)(mode & 0xfd);
        }

        public void Destroy()
        {
            if (send_evt != null)
            { try { send_evt.Dispose(); } catch { } finally { send_evt = null; } }

            if (ack_evt != null)
            { try { ack_evt.Dispose(); } catch { } finally { ack_evt = null; } }
        }
    }
}
