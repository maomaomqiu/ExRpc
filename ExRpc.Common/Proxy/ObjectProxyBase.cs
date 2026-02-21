using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEZ.Core.Server;
using ExRpc.Common.Protocol;

namespace ExRpc.Common.Proxy
{
    public class ObjectProxyBase:IObjectProxy
    {
        protected string _ObjectName = string.Empty;
        protected Communicator _Comm = null;
        protected HostInfo _Host = null;

        protected ObjectProxyBase(Communicator comm)
        {
            _Comm = comm;
        }

        protected ObjectProxyBase(Communicator comm, HostInfo host)
        {
            _Comm = comm;
            _Host = host;
        }

        protected int BeginRpcTransaction(IRpcCallMethod request, Transaction tran,out IRpcCallMethodReturn ackMsg)
        {
            bool modeConsistence = (tran.mode & Transaction.MODE_SEND_CONSISTENCE) > 0 ? true : false;
            bool modeWaitingAck = (tran.mode & Transaction.MODE_SEND_WAITING_ACK) > 0 ? true : false;

            //  初始化send事件对象
            if (modeConsistence)
                tran.send_evt = new AutoResetEvent(false);

            //  初始化ack事件对象
            if (modeWaitingAck)
                tran.ack_evt = new AutoResetEvent(false);

            int result = CommonSendWithRetry(request, tran);

            //  是否等待服务器响应
            if (!modeWaitingAck || result != RpcCodeDefines.SUCCESS)
            {
                //  注销事务
                if (tran.registered)
                    _Comm.UnRegisterTransaction(tran.__cb_id);

                //  不需要等待服务器响应,或者Request通信失败
                _Comm.CloseClient(_Host, tran.client);

                ackMsg = null;

                return result;
            }

            //  等待服务器响应或直到超时
            tran.ack_evt.WaitOne(_Comm.Config.RequestWaitingAckTimeout);
            if (tran.call_return != null)
            {
                //  获得返回数据
                //  需要代码生成器自动创建下列一行
                ackMsg = tran.call_return;
                result = RpcCodeDefines.SUCCESS;
            }
            else
            {
                ackMsg = null;
                result = RpcCodeDefines.RPC_ERR_ACK_SERVER_NO_RESPOND;
            }
                
            //  注销事务
            _Comm.UnRegisterTransaction(tran.__cb_id);
            _Comm.CloseClient(_Host, tran.client);

            return result;
        }

        protected int CommonSendWithRetry(IRpcCallMethod request, Transaction tran)
        {
            tran.send_retry++;

            if (tran.send_retry > _Comm.Config.RetryTimes)
                return RpcCodeDefines.RPC_ERR_REQ_SEND_FAIL_RETRY;

            int commRet = _Comm.SendMessage(_Host, request, tran);
            if (commRet == RpcCodeDefines.RPC_ERR_COMMUNICATOR_NO_CLIENT)
                return commRet;
            else if (commRet == RpcCodeDefines.RPC_ERR_COMMUNICATOR_CLIENT_CLOSED)
                return CommonSendWithRetry(request, tran);                              //  再次尝试

            bool modeConsistence = (tran.mode & Transaction.MODE_SEND_CONSISTENCE) > 0 ? true : false;
            //  是否确认发送结果
            if (modeConsistence)
            {
                //  确认发送结果或直到超时
                tran.send_evt.WaitOne(_Comm.Config.RequestTimeout);
            }
            else
            {
                //  不需要确认结果，直接赋值ok
                tran.status = RpcCodeDefines.TRAN_STATUS_OK;
            }

            if (tran.status == RpcCodeDefines.TRAN_STATUS_OK)
            {
                return RpcCodeDefines.SUCCESS;
            }

            //  尝试再次发送
            return CommonSendWithRetry(request, tran);
        }

        public void SetObjectName(string objName)
        {
            _ObjectName = objName;
        }

        public string GetObjectName()
        {
            return _ObjectName;
        }

        public string GetPhysicalObjectName()
        {
            return string.Format("{0}@{1}", _ObjectName, _Host.ToString());
        }

        public void SetHost(HostInfo host) { _Host = host; }
    }
}
