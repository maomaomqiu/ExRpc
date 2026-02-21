using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEZ.Utility.Configure;
using RabbitMQ;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ExRpc.Messaging.MQ;

namespace ExRpc.Messaging.ExEvent
{
    public class Subscriber : EventClient,IDisposable
    {
        private long _TotalSucc = 0;
        private long _Totals = 0;

        protected bool _Disposed = false;
        //protected event EventHandler<BasicDeliverEventArgs> OnReceivedHandler;
        protected RabbitMQClient _Client = null;
        public long Totals { get { return _Totals; } }
        public long TotalSucc { get { return _TotalSucc; } }

        public Subscriber(string evtName, Credential cred) : base(evtName, cred) { }

        public Subscriber(ExEventMeta evt, Credential cred) : base(evt, cred) { }

        ~Subscriber()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            //  释放所有的资源
            Dispose(true);
            //不需要再调用本对象的Finalize方法
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed)
                return;

            if (disposing)
            {
                //清理托管资源
                ReleaseClient();
            }

            _Disposed = true;
        }

        protected virtual void ReleaseClient()
        {
            RabbitMQClientFactory.ReleaseClient(_EventMeta.EventName, _Client);
        }

        public int ListenEvent(EventHandler<BasicDeliverEventArgs> recvHandler, IDictionary<string, object> args, string routingKey = "")
        {
            if (!ExEventDefines.IsPublishAllowed(_EventPermission))
                return ExEventDefines.RETURN_ERROR_NO_PERMISSION;

            _Client = RabbitMQClientFactory.GetClient(_EventMeta.EventName);
            if (_Client == null)
            {
                return ExEventDefines.RETURN_ERROR_NO_CLIENT;
            }

            string queueInstanceId = Guid.NewGuid().ToString().Replace("-", "").ToLower().Substring(0, 8);
            string queueName = string.Format("{0}{1}", _EventMeta.QueuePrefix, queueInstanceId);

            _Client.StartConsumer(_EventMeta.ExchangeName, queueName, "", _EventMeta.QueueDurable, _EventMeta.QueueExclusive, _EventMeta.QueueAutodelete, args, recvHandler);

            return ExEventDefines.RETURN_SUCCESS;
        }
    }
}
