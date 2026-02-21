using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExRpc.Common.Protocol;

namespace ExRpc.Common.Server
{
    public class ServantBase:IServant
    {
        protected NetEZ.Utility.Logger.Logger _Logger = null;
        protected string _ServantName = string.Empty;

        protected ServantBase()
        {
            //_ServantName = "ServantBase";
            _ServantName = this.GetType().Name;
        }

        public string GetServantName()
        {
            return _ServantName;
        }

        public virtual bool call_method(IRpcCallMethod request, out IRpcCallMethodReturn response)
        {
            response = null;
            return true;
        }

        public virtual void SetLogger(NetEZ.Utility.Logger.Logger logger)
        {
            _Logger = logger;
        }

        public void Error(string msg)
        {
            if (_Logger != null)
                _Logger.Error(msg);
        }

        public void Debug(string msg)
        {
            if (_Logger != null)
                _Logger.Debug(msg);
        }

        public void Info(string msg)
        {
            if (_Logger != null)
                _Logger.Info(msg);
        }
    }
}
