using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace ExRpc.Messaging.MQ
{
    public class RabbitMQConfigure
    {
        static char[] _Splitors = new char[] { ',', ';' };

        public List<AmqpTcpEndpoint> EndPoints;
        public string Username;
        public string Password;
        public string VirtualHost;

        public RabbitMQConfigure() { }
        public RabbitMQConfigure(string uriRaw, string vHost, string user, string pwd)
        {
            if (string.IsNullOrEmpty(uriRaw) || string.IsNullOrEmpty(vHost) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pwd))
                throw new Exception("Invalid uri or other required information.");

            EndPoints = ParseUriRaw(uriRaw);
            VirtualHost = vHost.Trim();
            Username = user.Trim();
            Password = pwd.Trim();
        }

        private List<AmqpTcpEndpoint> ParseUriRaw(string uriRaw)
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
    }
}
