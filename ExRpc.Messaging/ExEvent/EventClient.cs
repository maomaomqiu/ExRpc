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
    public class EventClient
    {
        protected Credential _Credential;
        protected ExEventMeta _EventMeta;
        protected int _EventPermission = ExEventDefines.CREDENTIAL_PERM_NONE;

        protected EventClient() { }

        protected EventClient(string evtName, Credential cred)
        {
            ExEventMeta evt = ExEventManager.GetEventMeta(evtName);
            
            if (cred == null || evt == null || string.IsNullOrEmpty(evt.EventName) || string.IsNullOrEmpty(evt.ExchangeName) || string.IsNullOrEmpty(evt.QueuePrefix))
                throw new Exception("The specific event or credential-account not found.");

            _Credential = cred;
            _EventMeta = evt;

            GetEventCredentialPerm(_EventMeta.EventName, _Credential.app_id, out _EventPermission);
        }

        protected EventClient(ExEventMeta evt, Credential cred)
        {
            if (cred == null || evt == null || string.IsNullOrEmpty(evt.EventName) || string.IsNullOrEmpty(evt.ExchangeName) || string.IsNullOrEmpty(evt.QueuePrefix))
                throw new Exception("Invalid params");

            _Credential = cred;
            _EventMeta = evt;

            GetEventCredentialPerm(_EventMeta.EventName, _Credential.app_id, out _EventPermission);
        }

        private bool CheckCredential()
        {
            //  暂时永远返回true
            return true;
        }

        private bool GetEventCredentialPerm(string evtName, string appId,out int permTag)
        {
            //  暂时永远返回true，全部权限（发布+订阅）
            permTag = ExEventDefines.CREDENTIAL_PERM_PUBLISH | ExEventDefines.CREDENTIAL_PERM_SUBSCRIBE;
            return true;
        }
    }
}
