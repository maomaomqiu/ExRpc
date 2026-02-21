using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExRpc.ZKAgent
{
    public class ClusterNodeInfo
    {
        private bool _IsOwner = false;
        private string _InstanceNodeId = string.Empty;

        public string RootName = string.Empty;
        public string ProjName = string.Empty;
        public string ClusterName = string.Empty;
        public string NodeName = string.Empty;
        public string Host = string.Empty;
        public int Port = 0;

        public bool IsOwner { get { return _IsOwner; } }
        public string InstanceNodeId { get { return _InstanceNodeId; } }
        public void SetIntanceNodeId(string id) { _InstanceNodeId = id; }

        public ClusterNodeInfo() { }

        public ClusterNodeInfo(string rootName, string projName, string clusterName, string nodeName)
        {
            RootName = rootName != null ? rootName.Trim().ToLower() : "";
            ProjName = projName != null ? projName.Trim().ToLower() : "";
            ClusterName = clusterName != null ? clusterName.Trim().ToLower() : "";
            NodeName = nodeName != null ? nodeName.Trim().ToLower() : "";

            if (string.IsNullOrEmpty(RootName) || string.IsNullOrEmpty(ProjName) || string.IsNullOrEmpty(ClusterName) || string.IsNullOrEmpty(NodeName))
                throw new Exception("Invalid proj-cluster-node name.");
        }

        public ClusterNodeInfo(string projName, string clusterName, string nodeName)
        {
            RootName = "wxroot";
            ProjName = projName != null ? projName.Trim().ToLower() : "";
            ClusterName = clusterName != null ? clusterName.Trim().ToLower() : "";
            NodeName = nodeName != null ? nodeName.Trim().ToLower() : "";

            if (string.IsNullOrEmpty(ProjName) || string.IsNullOrEmpty(ClusterName) || string.IsNullOrEmpty(NodeName))
                throw new Exception("Invalid proj-cluster-node name.");

        }

        public ClusterNodeInfo(string projName, string clusterName, string nodeName,string host,int port)
        {
            ProjName = projName != null ? projName.Trim().ToLower() : "";
            ClusterName = clusterName != null ? clusterName.Trim().ToLower() : "";
            NodeName = nodeName != null ? nodeName.Trim().ToLower() : "";

            Host = host != null ? host.Trim() : "";
            Port = port;

            if (string.IsNullOrEmpty(ProjName) || string.IsNullOrEmpty(ClusterName) || string.IsNullOrEmpty(NodeName))
                throw new Exception("Invalid proj-cluster-node name.");

            if (string.IsNullOrEmpty(Host) || Port < 1)
                throw new Exception("Invalid host or port.");
        }

        public void SetOwner()
        {
            _IsOwner = true;
        }
    }
}
