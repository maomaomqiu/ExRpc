using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;

namespace CodeMaker
{
    class ProxyCodeMaker
    {
        string proxyTemplate = @"using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExRpc.Common;
using ExRpc.Common.Protocol;
using ExRpc.Common.Proxy;
using NetEZ.Core.Server;
using QuestSvc.Protocol;
using QuestSvc.Protocol.Entity;
using QuestSvc.Protocol.RpcProtocol;

namespace QuestSvc.Client
{
    class #$servant$#Proxy : ObjectProxyBase
    {
        const string SERVANT_NAME = ""#$servant$#"";

        public #$servant$#Proxy(Communicator comm) : base(comm) { }
        public #$servant$#Proxy(Communicator comm, HostInfo host) : base(comm, host) { }

#$method_body$#
    }
}
";

        string methodParamAssignmentTemplate = "            request.#method_param# = #method_param#;";

        string methodBodyTemplate = @"        public int #$method$#(#$method_params_define$#out #$method$#Return ackMsg)
        {
            int result = RpcCodeDefines.RPC_ERR_REQ_BASE;

            ackMsg = null;

            #$method$#Request request = new #$method$#Request();

            request.__tid = 0;
            request.__servant = SERVANT_NAME;
            //  以下来自代码生成器,内容来自定义文件------------------->
            request.__method = ""#$method$#"";
#$method_params_assignment$#
            //<-------------------  以上来自代码生成器,内容来自定义文件

            Transaction tran = new Transaction();

            //  mode = 确认发送结果; 需要等待服务器响应
            //  来自代码生成器;赋值内容来自定义文件
            tran.mode = Transaction.MODE_SEND_CONSISTENCE | Transaction.MODE_SEND_WAITING_ACK;

            bool modeConsistence = (tran.mode & Transaction.MODE_SEND_CONSISTENCE) > 0 ? true : false;
            bool modeWaitingAck = (tran.mode & Transaction.MODE_SEND_WAITING_ACK) > 0 ? true : false;

            //  初始化send事件对象
            if (modeConsistence)
                tran.send_evt = new AutoResetEvent(false);

            //  初始化ack事件对象
            if (modeWaitingAck)
                tran.ack_evt = new AutoResetEvent(false);

            result = CommonSendWithRetry(request, tran);

            //  是否等待服务器响应
            if (!modeWaitingAck || result != RpcCodeDefines.SUCCESS)
            {
                //  注销事务
                if (tran.registered)
                    _Comm.UnRegisterTransaction(tran.__cb_id);

                //  不需要等待服务器响应,或者Request通信失败
                _Comm.CloseClient(_Host, tran.client);

                return result;
            }

            //  等待服务器响应或直到超时
            tran.ack_evt.WaitOne(_Comm.Config.RequestWaitingAckTimeout);
            if (tran.call_return != null && !tran.call_return.__err)
            {
                //  获得返回数据
                //  需要代码生成器自动创建下列一行
                ackMsg = (#$method$#Return) tran.call_return;
                result = RpcCodeDefines.SUCCESS;
            }
            else
                result = RpcCodeDefines.RPC_ERR_ACK_SERVER_NO_RESPOND;

            //  注销事务
            _Comm.UnRegisterTransaction(tran.__cb_id);
            _Comm.CloseClient(_Host, tran.client);

            return result;
        }";
        public int Start(string dllName, string servantName,string saveFolder)
        {
            //System.Reflection.Assembly assembly = System.Reflection.Assembly.Load("QuestSvc.Protocol");

            try
            {
                if (!dllName.EndsWith(".dll", StringComparison.CurrentCultureIgnoreCase))
                    dllName += ".dll";

                System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFrom(dllName);
                if (string.IsNullOrEmpty(saveFolder))
                    saveFolder = AppContext.BaseDirectory;
                //string savePath = string.Format("{0}{1}Proxy.cs", AppContext.BaseDirectory, servantName);
                string savePath = string.Format("{0}{1}Proxy.cs", saveFolder, servantName);

                if (assembly != null)
                {
                    Type[] types = assembly.GetTypes();

                    Dictionary<string, Type> methodTable = new Dictionary<string, Type>();

                    if (types != null && types.Length > 0)
                    {
                        StringBuilder allMethods = new StringBuilder();

                        foreach (Type type in types)
                        {
                            if (string.Compare(type.BaseType.Name, "RpcCallMethodBase", true) == 0)
                            {
                                string className = type.Name;
                                if (className.EndsWith("request", StringComparison.CurrentCultureIgnoreCase) && className.Length > 7)
                                {
                                    methodTable[className.Substring(0, className.Length - 7)] = type;
                                }
                                //else
                                //    methods.Add(className);
                            }
                        }

                        foreach (KeyValuePair<string, Type> item in methodTable)
                        {
                            string method = item.Key;
                            //string reqName = string.Format("{0}Request",method);
                            //string retName = string.Format("{0}Return", method);
                            Type reqClass = item.Value;

                            string paramDefines = string.Empty;
                            string paramAssignment = string.Empty;
                            string methodBody = string.Empty;

                            FieldInfo[] fieldInfos = reqClass.GetFields();
                            foreach (FieldInfo info in fieldInfos)
                            {
                                paramDefines += string.Format("{0} {1}, ", info.FieldType.Name, info.Name);
                                paramAssignment += methodParamAssignmentTemplate.Replace("#method_param#", info.Name) + "\r\n";
                            }

                            methodBody = methodBodyTemplate.Replace("#$method$#", method);
                            methodBody = methodBody.Replace("#$method_params_define$#", paramDefines);
                            methodBody = methodBody.Replace("#$method_params_assignment$#", paramAssignment);

                            allMethods.AppendLine("\r\n" + methodBody);
                        }

                        string proxyBody = proxyTemplate.Replace("#$servant$#", servantName);
                        proxyBody = proxyBody.Replace("#$method_body$#", allMethods.ToString()); ;

                        using (FileStream fs = new FileStream(savePath, FileMode.Create))
                        {
                            byte[] bytes = UTF8Encoding.UTF8.GetBytes(proxyBody);
                            fs.Write(bytes, 0, bytes.Length);
                            fs.Close();
                        }
                    }

                    Console.WriteLine("{0} saved.", savePath);

                    return 0;
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("加载失败.");
            }
            return -1;
        }
    }
}
