using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace ExRpc.Messaging.ExEvent
{
    public class ExEventMeta
    {
        public string EventName = string.Empty;

        public List<AmqpTcpEndpoint> MQEndPoints;
        public string MQVirtualHost = string.Empty;
        public string MQUser = string.Empty;
        public string MQPassword = string.Empty;

        public string ExchangeName = string.Empty;
        public string QueuePrefix = string.Empty;
        public bool Persistent;
        public bool QueueDurable;
        public bool QueueExclusive;
        public bool QueueAutodelete;
    }
}
