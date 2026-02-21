using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ExRpc.Messaging.MQ
{
    public class RabbitMQClient:NetEZ.Utility.Logger.LoggerBO, IDisposable
    {
        private bool _Disposed = false;
        private IConnection _Conn = null;
        private IModel _Channel = null;
        private IBasicProperties _PlainTextPersistentMsgProp = null;
        private IBasicProperties _PlainTextNonePersistentMsgProp = null;
        private RabbitMQConfigure _Config = null;

        public RabbitMQClient(string uri, string vHost, string user, string pwd)
        {
            _Config = new RabbitMQConfigure(uri, vHost, user, pwd);
            if (!InitConnection())
                throw new Exception("RabbitMQConfigure init failed.");
        }

        public RabbitMQClient(RabbitMQConfigure cfg)
        {
            if (cfg == null)
                throw new Exception("RabbitMQConfigure must be supplied.");

            _Config = cfg;
            if (!InitConnection())
                throw new Exception("RabbitMQConfigure init failed.");
        }

        public RabbitMQClient(string xconfigName)
        {
            NetEZ.Utility.Configure.SimpleXmlConfigure xconfig = NetEZ.Utility.Configure.SimpleXmlConfigure.GetConfig("rabbitmq.xml");
            if (xconfig == null)
                throw new Exception("xconfigName not found.");

            string uri = xconfig.GetItemValue(xconfigName, "Uri");
            string vHost = xconfig.GetItemValue(xconfigName, "VirtualHost");
            string user = xconfig.GetItemValue(xconfigName, "Username");
            string pwd = xconfig.GetItemValue(xconfigName, "Password");

            _Config = new RabbitMQConfigure(uri, vHost, user, pwd);
            if (!InitConnection())
                throw new Exception("RabbitMQConfigure init failed.");
        }

        ~RabbitMQClient()
        {
            Dispose(false);
        }

        public IBasicProperties GetPlainTextPersistentMsgProp()
        {
            if (_PlainTextPersistentMsgProp != null)
                return _PlainTextPersistentMsgProp;

            lock (this)
            {
                if (_PlainTextPersistentMsgProp != null)
                    return _PlainTextPersistentMsgProp;

                try 
                {
                    _PlainTextPersistentMsgProp = _Channel.CreateBasicProperties();
                    _PlainTextPersistentMsgProp.ContentType = "text/plain";
                    _PlainTextPersistentMsgProp.DeliveryMode = 2;
                }
                catch { }

                return _PlainTextPersistentMsgProp;
            }
        }

        public IBasicProperties GetPlainTextNonePersistentMsgProp()
        {
            if (_PlainTextNonePersistentMsgProp != null)
                return _PlainTextNonePersistentMsgProp;

            lock (this)
            {
                if (_PlainTextNonePersistentMsgProp != null)
                    return _PlainTextNonePersistentMsgProp;

                try
                {
                    _PlainTextNonePersistentMsgProp = _Channel.CreateBasicProperties();
                    _PlainTextNonePersistentMsgProp.ContentType = "text/plain";
                    _PlainTextNonePersistentMsgProp.DeliveryMode = 1;
                }
                catch { }

                return _PlainTextPersistentMsgProp;
            }
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
                Close();
            }

            _Disposed = true;
        }

        protected virtual bool InitConnection()
        {
            ConnectionFactory factory = new ConnectionFactory();
            
            factory.VirtualHost = _Config.VirtualHost;
            factory.UserName = _Config.Username;
            factory.Password = _Config.Password;
            _Conn = factory.CreateConnection(_Config.EndPoints);
            //_Conn = factory.CreateConnection("localhost");
            _Channel = _Conn.CreateModel();

            return true;
        }

        public void Close()
        {
            if (_Channel != null)
            {
                try
                {
                    _Channel.Close();
                    _Channel.Dispose();
                }
                catch { }
                finally
                {
                    _Channel = null;
                }
            }

            if (_Conn != null)
            {
                try
                {
                    _Conn.Close();
                    _Conn.Dispose();
                }
                catch { }
                finally
                {
                    _Conn = null;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="exchangeName"></param>
        /// <param name="routingKey"></param>
        /// <param name="msg"></param>
        /// <param name="deliveryMode">1=none persistent; 2=persistent</param>
        /// <returns></returns>
        public bool BasicPublish(string exchangeName,string routingKey, string msg, int deliveryMode = 1)
        {
            //  如果exchangeName\消息为空，不发消息，但返回true
            if (string.IsNullOrEmpty(msg) || string.IsNullOrEmpty(exchangeName))
                return true;

            IBasicProperties prop = deliveryMode == 1 ? GetPlainTextNonePersistentMsgProp() : GetPlainTextPersistentMsgProp();
            try 
            {
                byte[] msgBuf = System.Text.Encoding.UTF8.GetBytes(msg);
                _Channel.BasicPublish(exchangeName, routingKey, prop, msgBuf);

                return true;
            }
            catch (Exception ex)
            {
                LogError(string.Format("[RabbitMQClient] BasicPublish Exception: Ex={0}-{1}", ex.Message, ex.StackTrace));
            }
            finally { }

            return false;
        }

        public bool StartConsumer(string exchangeName,string queueName, string routingKey, bool durable,bool exclusive,bool autoDelete,IDictionary<string,object> arguments,EventHandler<BasicDeliverEventArgs> onReceivedHandler)
        {
            if (string.IsNullOrEmpty(exchangeName) || string.IsNullOrEmpty(queueName))
                return false;

            try 
            {
                QueueDeclareOk declareRet = _Channel.QueueDeclare(queueName, durable, exclusive, autoDelete, arguments);
                _Channel.QueueBind(queueName, exchangeName, routingKey, arguments);
                EventingBasicConsumer consumer = new EventingBasicConsumer(_Channel);
                _Channel.BasicConsume(queueName, true, consumer);
                consumer.Received += onReceivedHandler;

                return true;
            }
            catch (Exception ex)
            {
                LogError(string.Format("[RabbitMQClient] StartConsumer Exception: exchange={0}; queue={1}; Ex={0}-{1}", exchangeName, queueName, ex.Message, ex.StackTrace));
            }
            finally { }

            return false;
        }
    }
}
