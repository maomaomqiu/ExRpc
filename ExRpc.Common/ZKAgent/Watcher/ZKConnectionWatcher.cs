using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using org.apache.zookeeper;
using ExRpc.ZKAgent.Event;
using NetEZ.Utility.Logger;

namespace ExRpc.ZKAgent.Watcher
{
    public class ZKConnectionWatcher: org.apache.zookeeper.Watcher
    {
        private OnZKConnected _OnZKConnectedEventHandler = null;
        private OnZKDisConnected _OnZKDisconnectedEventHandler = null;
        protected volatile Logger _Logger = null;

        public void SetLogger(Logger logger)
        {
            _Logger = logger;
        }

        public void LogInfo(string msg)
        {
            if (_Logger != null)
                _Logger.Info(msg);
        }

        public void LogError(string msg)
        {
            if (_Logger != null)
                _Logger.Error(msg);
        }

        public void LogDebug(string msg)
        {
            if (_Logger != null)
                _Logger.Debug(msg);
        }

        public ZKConnectionWatcher(OnZKConnected connHandler,OnZKDisConnected disconnHandler)
        {
            if (connHandler == null)
                throw new Exception("Connected event handler must be supplied.");
            _OnZKConnectedEventHandler += connHandler;

            if (disconnHandler != null)
                _OnZKDisconnectedEventHandler += disconnHandler;
            
        }

        //public void Process(WatchedEvent evt)
        //{
        //    LogInfo(string.Format("zkserver connection watcher: state={0}; type={1}; path={2}", evt.State.ToString(), evt.Type.ToString(), evt.Path));

        //    //Console.WriteLine("zkserver watcher: state={0}; type={1}; path={2}", evt.State.ToString(), evt.Type.ToString(), evt.Path);

        //    if (evt.Type == EventType.None && evt.State == KeeperState.SyncConnected)
        //        _OnZKConnectedEventHandler(evt);

        //    if (evt.Type == EventType.None && evt.State == KeeperState.Disconnected && _OnZKDisconnectedEventHandler != null)
        //        _OnZKDisconnectedEventHandler(evt);

        //    //if (evt.State == KeeperState.SyncConnected)
        //    //{
        //    //    Console.WriteLine("ZKServer connected.");
        //    //}
        //}

        private void Process(WatchedEvent @event)
        {
            LogInfo(string.Format("zkserver connection watcher: state={0}; type={1}; path={2}", @event.getState().ToString(), @event.get_Type().ToString(), @event.getPath()));

            //Console.WriteLine("zkserver watcher: state={0}; type={1}; path={2}", evt.State.ToString(), evt.Type.ToString(), evt.Path);

            if (@event.get_Type() == org.apache.zookeeper.Watcher.Event.EventType.None && @event.getState() == org.apache.zookeeper.Watcher.Event.KeeperState.SyncConnected)
                _OnZKConnectedEventHandler(@event);

            if (@event.get_Type() == org.apache.zookeeper.Watcher.Event.EventType.None && @event.getState() == org.apache.zookeeper.Watcher.Event.KeeperState.Disconnected && _OnZKDisconnectedEventHandler != null)
                _OnZKDisconnectedEventHandler(@event);

            //if (evt.State == KeeperState.SyncConnected)
            //{
            //    Console.WriteLine("ZKServer connected.");
            //}
        }

        public override Task process(WatchedEvent @event)
        {
            Task t= new Task(() =>
                {
                    //LogInfo(string.Format("zkserver connection watcher: state={0}; type={1}; path={2}", @event.getState().ToString(), @event.get_Type().ToString(), @event.getPath()));
                    Process(@event);
                    //if (@event.get_Type() == org.apache.zookeeper.Watcher.Event.EventType.None && @event.getState() == org.apache.zookeeper.Watcher.Event.KeeperState.SyncConnected)
                    //    _OnZKConnectedEventHandler(@event);

                    //if (@event.get_Type() == org.apache.zookeeper.Watcher.Event.EventType.None && @event.getState() == org.apache.zookeeper.Watcher.Event.KeeperState.Disconnected && _OnZKDisconnectedEventHandler != null)
                    //    _OnZKDisconnectedEventHandler(@event);
                }
            );
            t.Start();
            return t;
        }
    }
}
