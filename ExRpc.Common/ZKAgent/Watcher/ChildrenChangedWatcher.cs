using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using org.apache.zookeeper;
using ExRpc.ZKAgent.Event;

namespace ExRpc.ZKAgent.Watcher
{
    //public class ChildrenChangedWatcher : org.apache.zookeeper.Watcher
    public class ChildrenChangedWatcher : org.apache.zookeeper.Watcher
    {
        private OnChildrenChanged _OnChildrenChangedEventHandler = null;

        public ChildrenChangedWatcher(OnChildrenChanged handler)
        {
            if (handler == null)
                throw new Exception("event handle must be supplied.");

            _OnChildrenChangedEventHandler += handler;
        }

        public override Task process(WatchedEvent @event)
        {
            Task t = new Task(() => _OnChildrenChangedEventHandler(@event));
            t.Start();
            return t;
        }

        //public void Process(WatchedEvent evt)
        //{
        //    //Console.WriteLine("children watcher: state={0}; type={1}; path={2}", evt.State.ToString(), evt.Type.ToString(), evt.Path);
        //    _OnChildrenChangedEventHandler(evt);
        //    //if (evt.State == KeeperState.SyncConnected)
        //    //{
        //    //    Console.WriteLine("ZKServer connected.");
        //    //}
        //}
    }
}
