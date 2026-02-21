using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using org.apache.zookeeper;

namespace ExRpc.ZKAgent.Event
{
    public delegate void OnZKConnected(WatchedEvent eventStat);
    public delegate void OnZKDisConnected(WatchedEvent eventStat);

    public delegate void OnChildrenChanged(WatchedEvent eventStat);
    //public delegate void OnServerDisConnectedEvent();
    public delegate void OnGNodeWatchedEvent(WatchedEvent eventStat);
    public delegate void OnClusterNodeChanged(List<ClusterNodeInfo> nodes);
}
