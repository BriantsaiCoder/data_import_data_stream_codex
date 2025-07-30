using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Sockets;

namespace DCT_data_import.Common
{
    /// <summary>
    /// 組態服務
    /// 統一組態管理和環境檢測邏輯
    /// </summary>
    public class ConfigurationService
    {
        private readonly WriteToLog _writeToLog;
        
        public ConfigurationService()
        {
            _writeToLog = new WriteToLog();
        }

        /// <summary>
        /// 環境配置類別
        /// </summary>
        public class EnvironmentConfig
        {
            public string Environment { get; set; }
            public string Host { get; set; }
            public string User { get; set; }
            public string Password { get; set; }
            public string Port { get; set; }
            public string Database { get; set; }
            public string PoolName { get; set; }
            public string FtpIp { get; set; }
            public string FtpUser { get; set; }
            public string FtpPassword { get; set; }
        }

        /// <summary>
        /// 取得環境設定
        /// </summary>
        public string GetEnvironment()
        {
            try
            {
                string localIp = GetLocalIPAddress();
                string[] productionIps = { "10.16.92.67", "10.16.92.68" };
                
                string environment = string.IsNullOrEmpty(localIp) ? "Dev" : 
                    (Array.Exists(productionIps, ip => ip == localIp) ? "Prod" : "Dev");

                _writeToLog.WriteToDataImportLog(string.Format("環境檢測結果: {0}", environment));
                return environment;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("環境檢測時發生錯誤: {0}", ex.Message));
                return "Dev";
            }
        }

        /// <summary>
        /// 取得本機 IPv4 地址
        /// </summary>
        private string GetLocalIPAddress()
        {
            try
            {
                string hostName = Dns.GetHostName();
                IPHostEntry hostEntry = Dns.GetHostEntry(hostName);
                
                foreach (IPAddress ip in hostEntry.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("取得本機 IP 時發生錯誤: {0}", ex.Message));
                return string.Empty;
            }
        }

        /// <summary>
        /// 載入環境配置
        /// </summary>
        public EnvironmentConfig LoadEnvironmentConfig()
        {
            try
            {
                string environment = GetEnvironment();
                
                var config = new EnvironmentConfig
                {
                    Environment = environment,
                    Host = ConfigurationManager.AppSettings[string.Format("{0}Host", environment)],
                    User = ConfigurationManager.AppSettings[string.Format("{0}UserName", environment)],
                    Password = ConfigurationManager.AppSettings[string.Format("{0}Password", environment)],
                    Port = ConfigurationManager.AppSettings[string.Format("{0}Port", environment)],
                    Database = ConfigurationManager.AppSettings[string.Format("{0}Database", environment)],
                    PoolName = ConfigurationManager.AppSettings["PoolName"],
                    FtpIp = ConfigurationManager.ConnectionStrings["FtpIp"]?.ConnectionString,
                    FtpUser = ConfigurationManager.ConnectionStrings["FtpUser"]?.ConnectionString,
                    FtpPassword = ConfigurationManager.ConnectionStrings["FtpPassword"]?.ConnectionString
                };

                ValidateConfiguration(config);
                return config;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("載入環境配置時發生錯誤: {0}", ex.Message));
                throw;
            }
        }

        /// <summary>
        /// 驗證配置完整性
        /// </summary>
        private void ValidateConfiguration(EnvironmentConfig config)
        {
            // 基本驗證
            if (string.IsNullOrEmpty(config.Host) || string.IsNullOrEmpty(config.User) || 
                string.IsNullOrEmpty(config.Database) || string.IsNullOrEmpty(config.FtpIp))
            {
                string message = "缺少必要配置項目";
                _writeToLog.WriteToDataImportLog(message);
                throw new ConfigurationErrorsException(message);
            }

            _writeToLog.WriteToDataImportLog("配置驗證通過");
        }

        /// <summary>
        /// 顯示配置資訊
        /// </summary>
        public void DisplayConfiguration(EnvironmentConfig config)
        {
            Console.WriteLine(string.Format("HOST: {0}", config.Host));
            Console.WriteLine(string.Format("USER: {0}", config.User));
            Console.WriteLine(string.Format("Environment: {0}", config.Environment));
            Console.WriteLine(string.Format("FTP IP: {0}", config.FtpIp));
        }
    }
}