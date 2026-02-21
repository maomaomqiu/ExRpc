using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExRpc.Common
{
    public class GridUri
    {
        
        private string _Root = string.Empty;
        private string _Proj = string.Empty;
        private string _Cluster = string.Empty;

        public string Root { get { return _Root; } set { _Root = value != null ? value.Trim().ToLower() : ""; } }
        public string Proj { get { return _Proj; } set { _Proj = value != null ? value.Trim().ToLower() : ""; } }
        public string Cluster { get { return _Cluster; } set { _Cluster = value != null ? value.Trim().ToLower() : ""; } }

        public string Scheme = "rpc";
        public GridUri() { }

        public GridUri(string root, string proj, string cluster)
        {
            Root = root;
            Proj = proj;
            Cluster = cluster;

            if (string.IsNullOrEmpty(_Root) || string.IsNullOrEmpty(_Proj) || string.IsNullOrEmpty(_Cluster))
                throw new Exception("Invalid params.");

            //  默认scheme
            Scheme = "rpc";
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Root) || string.IsNullOrEmpty(Proj) || string.IsNullOrEmpty(Cluster))
                return "";
            return string.Format("{0}.{1}.{2}", _Cluster, _Proj, _Root);
        }

        public string ToUriString()
        {
            return string.Format("{0}://{1}.{2}.{3}", Scheme, _Cluster, _Proj, _Root);
        }

        public static GridUri ParseFromString(string uriString)
        {
            GridUri result = null;
            if (uriString == null)
                return null;
            else
                uriString = uriString.Trim();

            if (uriString.Length < 1)
                return null;

            try
            {
                if (uriString.IndexOf("://") < 0)
                    uriString = "rpc://" + uriString;                    //  默认scheme
                
                Uri uri = new Uri(uriString);
                result = new GridUri();
                result.Scheme = uri.Scheme;
                
                string root =string.Empty;
                string proj = string.Empty;
                string cluster = string.Empty;


                int offset = uri.Host.Length - 1;
                int idx = uri.Host.LastIndexOf('.', offset);
                if (idx > 0 && idx < offset)
                    result.Root = uri.Host.Substring(idx + 1, offset - idx);
                else
                    return null;

                offset = idx - 1;
                idx = uri.Host.LastIndexOf('.', offset);
                if (idx > 0 && idx < offset)
                {
                    result.Proj = uri.Host.Substring(idx + 1, offset - idx);
                    result.Cluster = uri.Host.Substring(0, idx);
                }
                else
                {
                    //  只有 proj.root 字样
                    //result.Proj = uri.Host.Substring(0, offset).Trim().ToLower();
                    return null;
                }

                return result;
            }
            catch { }
            finally { }

            return null;
        }
    }
}
