using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetEZ.Utility.Configure;
using ExRpc.Messaging.MQ;

namespace ExRpc.Messaging.ExEvent
{
    public class Publisher : EventClient
    {
        private long _TotalSucc = 0;
        private long _Totals = 0;
        
        public long Totals { get { return _Totals; } }
        public long TotalSucc { get { return _TotalSucc; } }

        public Publisher(string evtName, Credential cred) : base(evtName, cred) { }

        public Publisher(ExEventMeta evt, Credential cred) : base(evt, cred) { }
        
        public int LaunchEvent(IEvent evtMsg,string routingKey = "")
        {
            if (!ExEventDefines.IsPublishAllowed(_EventPermission))
                return ExEventDefines.RETURN_ERROR_NO_PERMISSION;

            if (evtMsg == null)
                return ExEventDefines.RETURN_ERROR_EMPTY_MESSAGE;

            RabbitMQClient client = RabbitMQClientFactory.GetClient(_EventMeta.EventName);
            if (client == null)
            {
                return ExEventDefines.RETURN_ERROR_NO_CLIENT;
            }

            string msgJson = Newtonsoft.Json.JsonConvert.SerializeObject(evtMsg);
            //  累计发送总数
            Interlocked.Increment(ref _Totals);

            bool ret = client.BasicPublish(_EventMeta.ExchangeName, routingKey, msgJson, _EventMeta.Persistent ? 1 : 2);
            if (!ret)
            {
                RabbitMQClientFactory.DestroyClient(_EventMeta.EventName, client);
                return ExEventDefines.RETURN_ERROR_BASE;
            }
            else
            {
                //  累计发送成功总数
                Interlocked.Increment(ref _TotalSucc);
                RabbitMQClientFactory.ReleaseClient(_EventMeta.EventName, client);

                return ExEventDefines.RETURN_SUCCESS;
            }
        }

        public int LaunchEvents(IEvent[] evtMsgs, out int succ, string routingKey = "")
        {
            succ = 0;
            if (evtMsgs == null || evtMsgs.Length < 1)
                return ExEventDefines.RETURN_ERROR_EMPTY_MESSAGE;

            if (!ExEventDefines.IsPublishAllowed(_EventPermission))
                return ExEventDefines.RETURN_ERROR_NO_PERMISSION;

            RabbitMQClient client = null;

            foreach (IEvent msg in evtMsgs)
            {
                if (msg == null)
                    continue;

                if (client == null)
                {
                    client = RabbitMQClientFactory.GetClient(_EventMeta.EventName);
                    if (client == null)
                        return ExEventDefines.RETURN_ERROR_NO_CLIENT;
                }

                Interlocked.Increment(ref _Totals);

                string msgJson = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
                bool ret = client.BasicPublish(_EventMeta.ExchangeName, routingKey, msgJson, _EventMeta.Persistent ? 1 : 2);
                if (!ret)
                {
                    RabbitMQClientFactory.DestroyClient(_EventMeta.EventName, client);
                    client = null;
                }
                else
                {
                    Interlocked.Increment(ref _TotalSucc);
                    succ++;
                }
            }

            if (client != null)
            {
                RabbitMQClientFactory.ReleaseClient(_EventMeta.EventName, client);
            }

            return ExEventDefines.RETURN_SUCCESS;
        }
    }
}
