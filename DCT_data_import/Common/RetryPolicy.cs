using System;
using System.Threading;
using System.Threading.Tasks;

namespace DCT_data_import.Common
{
    /// <summary>
    /// 重試策略
    /// 提供網路操作和資料庫操作的重試機制
    /// </summary>
    public class RetryPolicy
    {
        private readonly WriteToLog _writeToLog;
        private readonly ExceptionHandler _exceptionHandler;

        public RetryPolicy()
        {
            _writeToLog = new WriteToLog();
            _exceptionHandler = new ExceptionHandler();
        }

        /// <summary>
        /// 重試配置
        /// </summary>
        public class RetryConfiguration
        {
            public int MaxRetryCount { get; set; } = 3;
            public int DelayMilliseconds { get; set; } = 1000;
            public bool UseExponentialBackoff { get; set; } = true;
            public int MaxDelayMilliseconds { get; set; } = 30000;

            public RetryConfiguration() { }

            public RetryConfiguration(int maxRetryCount, int delayMilliseconds, bool useExponentialBackoff = true)
            {
                MaxRetryCount = maxRetryCount;
                DelayMilliseconds = delayMilliseconds;
                UseExponentialBackoff = useExponentialBackoff;
            }
        }

        /// <summary>
        /// 重試結果
        /// </summary>
        public class RetryResult<T>
        {
            public bool Success { get; set; }
            public T Result { get; set; }
            public int AttemptCount { get; set; }
            public Exception LastException { get; set; }
            public string ErrorMessage { get; set; }

            public RetryResult(bool success, T result, int attemptCount, Exception lastException = null, string errorMessage = "")
            {
                Success = success;
                Result = result;
                AttemptCount = attemptCount;
                LastException = lastException;
                ErrorMessage = errorMessage;
            }
        }

        /// <summary>
        /// 執行重試操作
        /// </summary>
        public RetryResult<T> Execute<T>(Func<T> operation, RetryConfiguration config = null, string operationName = "")
        {
            if (config == null)
                config = new RetryConfiguration();

            int attemptCount = 0;
            Exception lastException = null;

            while (attemptCount <= config.MaxRetryCount)
            {
                attemptCount++;

                try
                {
                    _writeToLog.WriteToDataImportLog(string.Format("執行操作 '{0}' - 第 {1} 次嘗試", operationName, attemptCount));
                    
                    T result = operation();
                    
                    if (attemptCount > 1)
                    {
                        _writeToLog.WriteToDataImportLog(string.Format("操作 '{0}' 在第 {1} 次嘗試後成功", operationName, attemptCount));
                    }
                    
                    return new RetryResult<T>(true, result, attemptCount);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (attemptCount <= config.MaxRetryCount)
                    {
                        int delay = CalculateDelay(attemptCount, config);
                        _writeToLog.WriteToDataImportLog(string.Format("操作 '{0}' 第 {1} 次嘗試失敗: {2}，{3} 毫秒後重試", 
                            operationName, attemptCount, ex.Message, delay));
                        
                        Thread.Sleep(delay);
                    }
                    else
                    {
                        _writeToLog.WriteToDataImportLog(string.Format("操作 '{0}' 在 {1} 次嘗試後最終失敗: {2}", 
                            operationName, attemptCount, ex.Message));
                    }
                }
            }

            var exceptionResult = _exceptionHandler.HandleException(lastException, string.Format("重試操作 '{0}'", operationName));
            return new RetryResult<T>(false, default(T), attemptCount, lastException, exceptionResult.ErrorMessage);
        }

        /// <summary>
        /// 非同步執行重試操作
        /// </summary>
        public async Task<RetryResult<T>> ExecuteAsync<T>(Func<Task<T>> operation, RetryConfiguration config = null, string operationName = "")
        {
            if (config == null)
                config = new RetryConfiguration();

            int attemptCount = 0;
            Exception lastException = null;

            while (attemptCount <= config.MaxRetryCount)
            {
                attemptCount++;

                try
                {
                    _writeToLog.WriteToDataImportLog(string.Format("非同步執行操作 '{0}' - 第 {1} 次嘗試", operationName, attemptCount));
                    
                    T result = await operation();
                    
                    if (attemptCount > 1)
                    {
                        _writeToLog.WriteToDataImportLog(string.Format("非同步操作 '{0}' 在第 {1} 次嘗試後成功", operationName, attemptCount));
                    }
                    
                    return new RetryResult<T>(true, result, attemptCount);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (attemptCount <= config.MaxRetryCount)
                    {
                        int delay = CalculateDelay(attemptCount, config);
                        _writeToLog.WriteToDataImportLog(string.Format("非同步操作 '{0}' 第 {1} 次嘗試失敗: {2}，{3} 毫秒後重試", 
                            operationName, attemptCount, ex.Message, delay));
                        
                        await Task.Delay(delay);
                    }
                    else
                    {
                        _writeToLog.WriteToDataImportLog(string.Format("非同步操作 '{0}' 在 {1} 次嘗試後最終失敗: {2}", 
                            operationName, attemptCount, ex.Message));
                    }
                }
            }

            var exceptionResult = _exceptionHandler.HandleException(lastException, string.Format("非同步重試操作 '{0}'", operationName));
            return new RetryResult<T>(false, default(T), attemptCount, lastException, exceptionResult.ErrorMessage);
        }

        /// <summary>
        /// 計算延遲時間
        /// </summary>
        private int CalculateDelay(int attemptCount, RetryConfiguration config)
        {
            if (!config.UseExponentialBackoff)
            {
                return config.DelayMilliseconds;
            }

            // 指數退避算法
            int delay = config.DelayMilliseconds * (int)Math.Pow(2, attemptCount - 1);
            return Math.Min(delay, config.MaxDelayMilliseconds);
        }

        /// <summary>
        /// 網路操作重試配置
        /// </summary>
        public static RetryConfiguration NetworkRetryConfig => new RetryConfiguration(3, 2000, true);

        /// <summary>
        /// 資料庫操作重試配置
        /// </summary>
        public static RetryConfiguration DatabaseRetryConfig => new RetryConfiguration(2, 1000, false);

        /// <summary>
        /// 檔案操作重試配置
        /// </summary>
        public static RetryConfiguration FileRetryConfig => new RetryConfiguration(3, 500, false);
    }
}