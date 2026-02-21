using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using NetEZ.Utility.Logger;
using NetEZ.Utility.Tools;
using NetEZ.Utility.Configure;
using Newtonsoft.Json;

namespace ExRpc.Common.Server
{
    internal class PerformanceItem
    {
        public string uri;
        public string instance;
        public string servant;
        public string method;
        public int cnt_call = 0;
        public int cnt_fail = 0;

        /// <summary>
        /// 执行时间，单位：微秒
        /// </summary>
        public long sum_elapse = 0;

        public PerformanceItem() { }
        public PerformanceItem(string uriStr, string instanceId, string servantStr, string methodStr)
        {
            uri = uriStr;
            instance = instanceId;
            servant = servantStr;
            method = methodStr;
        }

        public string GetUniqueId()
        {
            return string.Format("{0}-{1}-{2}-{3}", uri, instance, servant, method);
        }

        public void IncrementCntCall(int val = 1)
        {
            if (val == 1)
                Interlocked.Increment(ref cnt_call);
            else
                Interlocked.Add(ref cnt_call, val);
        }

        public void IncrementCntFail(int val = 1)
        {
            if (val == 1)
                Interlocked.Increment(ref cnt_fail);
            else
                Interlocked.Add(ref cnt_fail, val);
        }

        public void IncrementSumElapse(long val)
        {
            Interlocked.Add(ref sum_elapse, val);
        }
    }

    internal class PerformanceRecorder:LoggerBO
    {
        static volatile string _ConnString = string.Empty;
        static object _Locker = new object();

        private int _SavingInterval = 1000 * 300;
        private volatile bool _IsThreadRunning = false;
        private Thread _SavingThread = null;
        private ConcurrentDictionary<string, PerformanceItem> _PerformanceItemTable = new ConcurrentDictionary<string, PerformanceItem>();

        public PerformanceRecorder()
        {
            //  保存间隔默认为5分钟
            _SavingInterval = 1000 * 300;

            InitThread();
        }

        public PerformanceRecorder(int savingInterval)
        { 
            //  保存间隔至少为5s
            _SavingInterval = savingInterval >= 5000 ? savingInterval : 1000 * 300;
            
            InitThread();
        }

        private void InitThread()
        {
            _IsThreadRunning = true;
            _SavingThread = new Thread(SavingProc);
            _SavingThread.IsBackground = true;
            _SavingThread.Start();
            while (!_SavingThread.IsAlive)
                Thread.Sleep(3);
        }

        /// <summary>
        /// 一次性累计多项数据
        /// </summary>
        /// <param name="uriStr"></param>
        /// <param name="instanceId"></param>
        /// <param name="servantStr"></param>
        /// <param name="methodStr"></param>
        /// <param name="cntCall"></param>
        /// <param name="cntFail"></param>
        /// <param name="elapse">执行时间，单位：微秒</param>
        public void Increment(string uriStr, string instanceId, string servantStr, string methodStr, int cntCall, int cntFail, long elapse)
        {
            uriStr = uriStr.Trim().ToLower();
            instanceId = instanceId.Trim().ToLower();
            servantStr = servantStr.Trim().ToLower();
            methodStr = methodStr.Trim().ToLower();

            string key = string.Format("{0}-{1}-{2}-{3}", uriStr, instanceId, servantStr, methodStr);
            PerformanceItem pi = null;
            if (_PerformanceItemTable.TryGetValue(key, out pi))
            {
                if (cntCall > 0)
                    pi.IncrementCntCall(cntCall);
                if (cntFail > 0)
                    pi.IncrementCntFail(cntFail);
                if (elapse > 0)
                    pi.IncrementSumElapse(elapse);
            }
            else
            {
                pi = new PerformanceItem(uriStr, instanceId, servantStr, methodStr);
                if (cntCall > 0)
                    pi.IncrementCntCall(cntCall);
                if (cntFail > 0)
                    pi.IncrementCntFail(cntFail);
                if (elapse > 0)
                    pi.IncrementSumElapse(elapse);

                _PerformanceItemTable.AddOrUpdate(key, pi, (k, v) => 
                { 
                    if (cntCall > 0)
                        v.IncrementCntCall(cntCall);
                    if (cntFail > 0)
                        v.IncrementCntFail(cntFail);
                    if (elapse > 0)
                        v.IncrementSumElapse(elapse);
                    return v; 
                });
            }
        }

        public void IncrementCntCall(string uriStr, string instanceId, string servantStr, string methodStr, int val)
        {
            uriStr = uriStr.Trim().ToLower();
            instanceId = instanceId.Trim().ToLower();
            servantStr = servantStr.Trim().ToLower();
            methodStr = methodStr.Trim().ToLower();

            string key = string.Format("{0}-{1}-{2}-{3}", uriStr, instanceId, servantStr, methodStr);
            PerformanceItem pi = null;
            if (_PerformanceItemTable.TryGetValue(key, out pi))
            {
                pi.IncrementCntCall(val);
            }
            else
            {
                pi = new PerformanceItem(uriStr, instanceId, servantStr, methodStr);
                pi.IncrementCntCall(val);
                _PerformanceItemTable.AddOrUpdate(key, pi, (k, v) => { v.IncrementCntCall(val); return v; });
            }
        }

        public void IncrementCntFail(string uriStr, string instanceId, string servantStr, string methodStr, int val)
        {
            uriStr = uriStr.Trim().ToLower();
            instanceId = instanceId.Trim().ToLower();
            servantStr = servantStr.Trim().ToLower();
            methodStr = methodStr.Trim().ToLower();

            string key = string.Format("{0}-{1}-{2}-{3}", uriStr, instanceId, servantStr, methodStr);
            PerformanceItem pi = null;
            if (_PerformanceItemTable.TryGetValue(key, out pi))
            {
                pi.IncrementCntFail(val);
            }
            else
            {
                pi = new PerformanceItem(uriStr, instanceId, servantStr, methodStr);
                pi.IncrementCntCall(val);
                _PerformanceItemTable.AddOrUpdate(key, pi, (k, v) => { v.IncrementCntFail(val); return v; });
            }
        }

        public void IncrementSumElapse(string uriStr, string instanceId, string servantStr, string methodStr, long val)
        {
            uriStr = uriStr.Trim().ToLower();
            instanceId = instanceId.Trim().ToLower();
            servantStr = servantStr.Trim().ToLower();
            methodStr = methodStr.Trim().ToLower();

            string key = string.Format("{0}-{1}-{2}-{3}", uriStr, instanceId, servantStr, methodStr);
            PerformanceItem pi = null;
            if (_PerformanceItemTable.TryGetValue(key, out pi))
            {
                pi.IncrementSumElapse(val);
            }
            else
            {
                pi = new PerformanceItem(uriStr, instanceId, servantStr, methodStr);
                pi.IncrementSumElapse(val);
                _PerformanceItemTable.AddOrUpdate(key, pi, (k, v) => { v.IncrementSumElapse(val); return v; });
            }
        }

        private void SavingProc(object state)
        {
            List<string> keysBuf = new List<string>(1024);
            PerformanceItem pi = null;
            while (_IsThreadRunning)
            {
                Thread.Sleep(_SavingInterval);

                try
                {
                    foreach (string key in _PerformanceItemTable.Keys)
                    {
                        keysBuf.Add(key);
                    }

                    if (keysBuf.Count < 1)
                        continue;
                    
                    int saved = 0;
                    int failed = 0;
                    foreach (string key in keysBuf)
                    {
                        if (!_PerformanceItemTable.TryRemove(key, out pi))
                            continue;

                        if (SaveToDb(pi) != 0)
                        {
                            //  保存失败，放回去
                            _PerformanceItemTable.AddOrUpdate(pi.GetUniqueId(), pi, (k, v) => { v.IncrementCntCall(pi.cnt_call); v.IncrementCntFail(pi.cnt_fail); v.IncrementSumElapse(pi.sum_elapse); return v; });
                            failed++;
                        }
                        else
                            saved++;
                    }

                    LogInfo(string.Format("[PerformanceRecorder] SavingProc: Saved={0}; Failed={1}", saved, failed));
                }
                catch(Exception ex)
                {
                    LogError(string.Format("[PerformanceRecorder] SavingProc Exception: Ex={0}-{1}", ex.Message, ex.StackTrace));
                }
                finally 
                {
                    if (keysBuf.Count > 0)
                        keysBuf.Clear();
                }
                
            }
        }

        private static string GetConnString()
        {
            if (!string.IsNullOrEmpty(_ConnString))
                return _ConnString;

            lock (_Locker)
            {
                if (!string.IsNullOrEmpty(_ConnString))
                    return _ConnString;

                try 
                {
                    SimpleXmlConfigure cfg = SimpleXmlConfigure.GetConfig("mssql.xml");
                    _ConnString = cfg.GetItemValue("wx_stat_user_main", 0);
                }
                catch { }
                finally { }

                return _ConnString;
            }
        }

        private SqlConnection GetConnection()
        {
            SqlConnection conn = null;

            try
            {
                string connString = GetConnString();
                if (string.IsNullOrEmpty(connString))
                    return null;

                conn = new SqlConnection(connString);
                conn.Open();
            }
            catch (Exception ex)
            {
                conn = null;
            }
            finally
            {
            }

            return conn;
        }

        private int SaveToDb(PerformanceItem pi)
        {
            if (pi == null || string.IsNullOrEmpty(pi.uri) || string.IsNullOrEmpty(pi.instance))
                return 0;

            try
            {
                string sql = "insert into log_rpcserver_performance (uri,instance,servant,method,cnt_call,cnt_fail,sum_elapse) values(@uri,@instance,@servant,@method,@cnt_call,@cnt_fail,@sum_elapse)";
                using (SqlConnection conn = GetConnection())
                {
                    if (conn == null)
                    {
                        LogError(string.Format("[PerformanceRecorder] SaveToDb fail: no db-connection."));
                        return 500;
                    }
                        

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.CommandType = CommandType.Text;

                        SqlParameter pUri = new SqlParameter("@uri", SqlDbType.VarChar, 256);
                        pUri.Value = pi.uri;

                        SqlParameter pInstance = new SqlParameter("@instance", SqlDbType.VarChar, 128);
                        pInstance.Value = pi.instance;

                        SqlParameter pServant = new SqlParameter("@servant", SqlDbType.VarChar, 128);
                        pServant.Value = pi.servant;

                        SqlParameter pMethod = new SqlParameter("@method", SqlDbType.VarChar, 128);
                        pMethod.Value = pi.method;

                        SqlParameter pCntCall = new SqlParameter("@cnt_call", pi.cnt_call);
                        SqlParameter pCntFail = new SqlParameter("@cnt_fail", pi.cnt_fail);
                        SqlParameter pSumElapse = new SqlParameter("@sum_elapse", pi.sum_elapse);            //  执行时间，单位：微秒

                        cmd.Parameters.Add(pUri);
                        cmd.Parameters.Add(pInstance);
                        cmd.Parameters.Add(pServant);
                        cmd.Parameters.Add(pMethod);
                        cmd.Parameters.Add(pCntCall);
                        cmd.Parameters.Add(pCntFail);
                        cmd.Parameters.Add(pSumElapse);

                        cmd.ExecuteNonQuery();

                        return 0;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format("[PerformanceRecorder] SaveToDb Exception: Ex={0}-{1}", ex.Message, ex.StackTrace));
            }
            finally { }

            return 500;
        }

    }
}
