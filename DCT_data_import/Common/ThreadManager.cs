using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DCT_data_import.Common;

namespace DCT_data_import.Common
{
    /// <summary>
    /// 執行緒管理器
    /// 統一管理應用程式中的多執行緒邏輯
    /// </summary>
    public class ThreadManager
    {
        private readonly WriteToLog _writeToLog;
        
        /// <summary>
        /// 執行緒資訊類別
        /// </summary>
        public class ThreadInfo
        {
            public string Name { get; set; }
            public Thread Thread { get; set; }
            public bool IsAlive => Thread != null && Thread.IsAlive;
            public ThreadStart ThreadStart { get; set; }
            
            public ThreadInfo(string name, ThreadStart threadStart)
            {
                Name = name;
                ThreadStart = threadStart;
                Thread = new Thread(threadStart);
            }
            
            public void Start()
            {
                if (Thread != null && !Thread.IsAlive)
                {
                    Thread = new Thread(ThreadStart);
                    Thread.Start();
                }
            }
            
            public void Stop()
            {
                if (Thread != null && Thread.IsAlive)
                {
                    try
                    {
                        Thread.Interrupt();
                        Thread.Abort();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("停止執行緒 {0} 時發生錯誤: {1}", Name, ex.Message));
                    }
                }
            }
        }

        private readonly Dictionary<string, ThreadInfo> _threads;

        public ThreadManager()
        {
            _writeToLog = new WriteToLog();
            _threads = new Dictionary<string, ThreadInfo>();
        }

        /// <summary>
        /// 註冊執行緒
        /// </summary>
        public void RegisterThread(string threadName, ThreadStart threadStart)
        {
            if (!_threads.ContainsKey(threadName))
            {
                _threads[threadName] = new ThreadInfo(threadName, threadStart);
                _writeToLog.WriteToDataImportLog(string.Format("執行緒 {0} 已註冊", threadName));
            }
        }

        /// <summary>
        /// 啟動執行緒
        /// </summary>
        public bool StartThread(string threadName)
        {
            if (_threads.ContainsKey(threadName))
            {
                try
                {
                    _threads[threadName].Start();
                    _writeToLog.WriteToDataImportLog(string.Format("執行緒 {0} 已啟動", threadName));
                    return true;
                }
                catch (Exception ex)
                {
                    _writeToLog.WriteToDataImportLog(string.Format("啟動執行緒 {0} 失敗: {1}", threadName, ex.Message));
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 停止執行緒
        /// </summary>
        public bool StopThread(string threadName)
        {
            if (_threads.ContainsKey(threadName))
            {
                try
                {
                    _threads[threadName].Stop();
                    _writeToLog.WriteToDataImportLog(string.Format("執行緒 {0} 已停止", threadName));
                    return true;
                }
                catch (Exception ex)
                {
                    _writeToLog.WriteToDataImportLog(string.Format("停止執行緒 {0} 失敗: {1}", threadName, ex.Message));
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 檢查執行緒是否存活
        /// </summary>
        public bool IsThreadAlive(string threadName)
        {
            return _threads.ContainsKey(threadName) && _threads[threadName].IsAlive;
        }

        /// <summary>
        /// 重啟執行緒
        /// </summary>
        public bool RestartThread(string threadName)
        {
            if (_threads.ContainsKey(threadName))
            {
                StopThread(threadName);
                Thread.Sleep(1000); // 等待執行緒完全停止
                return StartThread(threadName);
            }
            return false;
        }

        /// <summary>
        /// 管理所有註冊的執行緒
        /// </summary>
        public void ManageAllThreads()
        {
            var threadNames = _threads.Keys.ToList();
            
            foreach (string threadName in threadNames)
            {
                Console.WriteLine(string.Format("{0} IsAlive: {1}", threadName, IsThreadAlive(threadName)));
                
                if (!IsThreadAlive(threadName))
                {
                    RestartThread(threadName);
                }
            }
        }

        /// <summary>
        /// 取得執行緒狀態報告
        /// </summary>
        public Dictionary<string, bool> GetThreadStatusReport()
        {
            var report = new Dictionary<string, bool>();
            foreach (var kvp in _threads)
            {
                report[kvp.Key] = kvp.Value.IsAlive;
            }
            return report;
        }

        /// <summary>
        /// 停止所有執行緒
        /// </summary>
        public void StopAllThreads()
        {
            foreach (var threadName in _threads.Keys.ToList())
            {
                StopThread(threadName);
            }
        }
    }
}