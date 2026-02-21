using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExRpc.Messaging.ExEvent
{
    public class Credential
    {
        public string app_id = string.Empty;
        public string app_key = string.Empty;
        public string app_secret = string.Empty;

        public Credential() { }
        public Credential(string appId, string appKey, string appSecret)
        {
            app_id = appId;
            app_key = appKey;
            app_secret = appSecret;
        }
    }

    public enum CredentialPermission
    { 
        None = 0,

    }
}
