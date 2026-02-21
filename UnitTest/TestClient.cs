using System;
using System.Collections.Generic;
using System.Text;
using ExRpc.ZKAgent;

namespace UnitTest
{
    class TestClient
    {
        public void Start()
        {
            ZKConnectInfo conn = new ZKConnectInfo("192.168.13.138:2181");
            ExRpc.ZKAgent.ZKClient client = new ExRpc.ZKAgent.ZKClient(conn);
            client.ConnectToZKServer(true);
            byte[] buf = null;
            ZKReturn ret = client.GetData("/proj-cpc", null, out buf);

            Console.WriteLine("ret={0}; msg={1}", ret.Code, ret.Msg);
        }
    }
}
