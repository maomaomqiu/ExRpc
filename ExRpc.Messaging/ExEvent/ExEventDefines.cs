using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExRpc.Messaging.ExEvent
{
    public class ExEventDefines
    {
        public const int CREDENTIAL_PERM_NONE = 0;
        public const int CREDENTIAL_PERM_PUBLISH = 1;
        public const int CREDENTIAL_PERM_SUBSCRIBE = 2;

        public const int RETURN_SUCCESS = 0;
        public const int RETURN_ERROR_BASE = 1000;
        public const int RETURN_ERROR_NO_CLIENT = RETURN_ERROR_BASE + 1;
        public const int RETURN_ERROR_EMPTY_MESSAGE = RETURN_ERROR_BASE + 2;
        public const int RETURN_ERROR_NO_PERMISSION = RETURN_ERROR_BASE + 3;

        public static bool IsPublishAllowed(int perm)
        {
            return (perm & CREDENTIAL_PERM_PUBLISH) > 0 ? true : false;
        }

        public static bool IsSubscribeAllowed(int perm)
        {
            return (perm & CREDENTIAL_PERM_SUBSCRIBE) > 0 ? true : false;
        }
    }


}
