using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using NetEZ.Utility.Configure;

namespace ExRpc.Messaging.ExEvent
{
    public class ExEventManager
    {
        static char[] _Splitors = new char[] { ',', ';' };
        static ConcurrentDictionary<string, ExEventMeta> _EventsTable = new ConcurrentDictionary<string, ExEventMeta>();

        private static List<AmqpTcpEndpoint> ParseUriRaw(string uriRaw)
        {
            if (string.IsNullOrEmpty(uriRaw))
                return null;

            string[] uris = uriRaw.Split(_Splitors, StringSplitOptions.RemoveEmptyEntries);
            if (uris == null || uris.Length < 1)
                throw new Exception("Invalid uri.");

            List<AmqpTcpEndpoint> endpoints = null;
            foreach (string uri in uris)
            {
                Uri uriTmp = new Uri(uri);

                if (endpoints == null)
                    endpoints = new List<AmqpTcpEndpoint>();
                endpoints.Add(new AmqpTcpEndpoint(uriTmp.Host, uriTmp.Port));
            }

            return endpoints;
        }

        public static ExEventMeta GetEventMeta(string evtName)
        {
            evtName = evtName != null ? evtName.Trim().ToLower() : "";
            if (evtName.Length < 1)
                return null;

            ExEventMeta evt = null;
            if (_EventsTable.TryGetValue(evtName, out evt))
                return evt;

            SimpleXmlConfigure xConfig = SimpleXmlConfigure.GetConfig("rabbitmq.xml");
            if (xConfig != null)
            {
                List<AmqpTcpEndpoint> endpoints = ParseUriRaw(xConfig.GetItemValue(evtName, "Uri"));
                string virtualHost = xConfig.GetItemValue(evtName, "VirtualHost");
                string userName = xConfig.GetItemValue(evtName, "UserName");
                string pwd = xConfig.GetItemValue(evtName, "Password");

                string exchangeName = xConfig.GetItemValue(evtName, "Exchange");
                if (string.IsNullOrEmpty(exchangeName))
                    return null;
                string queuePrefix = xConfig.GetItemValue(evtName, "QueuePrefix");
                if (string.IsNullOrEmpty(queuePrefix))
                    return null;
                string persistent = xConfig.GetItemValue(evtName, "QueuePersistent");
                string queueDurable = xConfig.GetItemValue(evtName, "QueueDurable");
                string queueExclusive = xConfig.GetItemValue(evtName, "QueueExclusive");
                string queueAutodelete = xConfig.GetItemValue(evtName, "QueueAutoDelete");

                evt = new ExEventMeta();
                evt.EventName = evtName;
                evt.MQEndPoints = endpoints;
                evt.MQVirtualHost = virtualHost;
                evt.MQUser = userName;
                evt.MQPassword = pwd;
                evt.ExchangeName = exchangeName != null ? exchangeName.Trim():"";
                evt.QueuePrefix = queuePrefix != null ? queuePrefix.Trim() : "";
                evt.Persistent = !string.IsNullOrEmpty(persistent) && string.Compare(persistent, "true", true) == 0 ? true : false;
                evt.QueueDurable = !string.IsNullOrEmpty(queueDurable) && string.Compare(queueDurable, "true", true) == 0 ? true : false;
                evt.QueueExclusive = !string.IsNullOrEmpty(queueExclusive) && string.Compare(queueExclusive, "true", true) == 0 ? true : false;
                evt.QueueAutodelete = !string.IsNullOrEmpty(queueAutodelete) && string.Compare(queueAutodelete, "true", true) == 0 ? true : false;

                _EventsTable.AddOrUpdate(evtName, evt, (k, v) => v);

                return evt;
            }

            return null;
        }
    }
}
