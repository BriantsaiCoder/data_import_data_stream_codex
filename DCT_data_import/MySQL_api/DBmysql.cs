using System;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using static DCT_data_import.ApiObject;
using System.Collections.Generic;
using System.Threading;
using Dapper;
namespace DCT_data_import
{
    public class DBmysql
    {
        // 使用讀寫鎖來提供更好的併發效能
        private static readonly ReaderWriterLockSlim readWriteLock = new ReaderWriterLockSlim();
        public void Connect(string IP, string Port, string user, string password, string dbName)
        {
            MySqlConnectionManager.Initialize(IP, Port, user, password, dbName);
        }
        public Pool_execute_response Excute_mysql_cmd(string cmd_string, string mode = "select", object parameters = null)
        {
            Pool_execute_response response = new Pool_execute_response();
            response.Data = new JArray();
            try
            {
                using (MySqlConnection connection = new MySqlConnection(MySqlConnectionManager.ConnectionString))
                {
                    connection.Open();
                    if (mode.ToLower() == "select")
                    {
                        ExecuteSelectCommand(connection, cmd_string, parameters, response);
                    }
                    else
                    {
                        ExecuteNonQueryCommand(connection, cmd_string, parameters, response);
                    }
                }
            }
            catch (MySqlException mysqlEx)
            {
                response.Error = FormatMySqlError(mysqlEx);
            }
            catch (Exception ex)
            {
                response.Error = $"執行資料庫操作時發生錯誤: {ex.Message}";
            }
            return response;
        }
        private void ExecuteSelectCommand(MySqlConnection connection, string cmd_string, object parameters, Pool_execute_response response)
        {
            // 使用讀鎖來允許多個 select 同時執行
            readWriteLock.EnterReadLock();
            try
            {
                var results = connection.Query(cmd_string, parameters);
                JArray jsonArray = new JArray();
                foreach (var row in results)
                {
                    JObject jsonRow = new JObject();
                    var rowDict = row as IDictionary<string, object>;
                    if (rowDict != null)
                    {
                        foreach (var kvp in rowDict)
                        {
                            jsonRow[kvp.Key] = JToken.FromObject(kvp.Value ?? DBNull.Value);
                        }
                    }
                    jsonArray.Add(jsonRow);
                }
                response.Data = jsonArray;
            }
            finally
            {
                readWriteLock.ExitReadLock();
            }
        }
        private void ExecuteNonQueryCommand(MySqlConnection connection, string cmd_string, object parameters, Pool_execute_response response)
        {
            // 使用寫鎖來確保 insert/update/delete 操作的安全性
            readWriteLock.EnterWriteLock();
            try
            {
                int affectedRows = connection.Execute(cmd_string, parameters);
                // 取得最後插入的 ID（如果適用）
                long insertId = 0;
                if (cmd_string.Trim().ToUpper().StartsWith("INSERT"))
                {
                    insertId = connection.QuerySingleOrDefault<long>("SELECT LAST_INSERT_ID()");
                }
                JObject resultObj = new JObject
                {
                    ["fieldCount"] = 0,
                    ["affectedRows"] = affectedRows,
                    ["insertId"] = insertId,
                    ["info"] = "",
                    ["serverStatus"] = 2,
                    ["warningStatus"] = 0
                };
                response.Data.Add(resultObj);
            }
            finally
            {
                readWriteLock.ExitWriteLock();
            }
        }
        private string FormatMySqlError(MySqlException mysqlEx)
        {
            string errorMessage = $"MySQL 錯誤 (代碼: {mysqlEx.Number}): {mysqlEx.Message}";
            // 根據常見的 MySQL 錯誤代碼提供更詳細的說明
            switch (mysqlEx.Number)
            {
                case 1062:
                    errorMessage += " - 重複鍵值錯誤";
                    break;
                case 1452:
                    errorMessage += " - 外鍵約束失敗";
                    break;
                case 1146:
                    errorMessage += " - 資料表不存在";
                    break;
                case 1054:
                    errorMessage += " - 欄位不存在";
                    break;
                case 1064:
                    errorMessage += " - SQL 語法錯誤";
                    break;
                case 2006:
                    errorMessage += " - MySQL 伺服器已斷線";
                    break;
                case 1045:
                    errorMessage += " - 存取被拒絕，請檢查使用者名稱和密碼";
                    break;
                case 1049:
                    errorMessage += " - 未知的資料庫";
                    break;
                default:
                    errorMessage += $" - 詳細錯誤: {mysqlEx.GetType().Name}";
                    break;
            }
            return errorMessage;
        }
        // 清理資源
        public static void Dispose()
        {
            readWriteLock?.Dispose();
        }
    }
    public class MySqlConnectionManager
    {
        private static string connectionString;
        public static string ConnectionString { get { return connectionString; } }
        public static void Initialize(string IP, string Port, string user, string password, string dbName)
        {
            // 改善連接字串的安全性設定
            connectionString = $"server={IP};port={Port};user id={user};password={password};database={dbName};" +
                              $"sslMode=Preferred;Pooling=true;Min Pool Size=5;Max Pool Size=100;" +
                              $"Connection Lifetime=0;Connection Timeout=30;Command Timeout=60;" +
                              $"Allow User Variables=true;Convert Zero Datetime=true;";
        }
    }
}
//namespace DCT_data_import
//{
//    public class DBmysql
//    {
//        private static readonly object lockObj = new object();
//        public void Connect(string IP, string Port, string user, string password, string dbName)
//        {
//            MySqlConnectionManager.Initialize(IP, Port, user, password, dbName);
//        }
//        public Pool_execute_response Excute_mysql_cmd(string cmd_string, string mode = "select")
//        {
//            Pool_execute_response response = new Pool_execute_response();
//            response.Data = new JArray();
//            try
//            {
//                // 使用新的連線物件
//                using (MySqlConnection localConnection = new MySqlConnection(MySqlConnectionManager.ConnectionString))
//                {
//                    localConnection.Open();
//                    using (MySqlCommand SQLcmd = new MySqlCommand(cmd_string, localConnection))
//                    {
//                        if (mode == "select")
//                        {
//                            lock (lockObj) // 加鎖，確保執行緒安全
//                            {
//                                using (MySqlDataReader receiveSQLdata = SQLcmd.ExecuteReader())
//                                {
//                                    // 將資料存儲到 JArray
//                                    JArray jsonArray = new JArray();
//                                    while (receiveSQLdata.Read())
//                                    {
//                                        JObject row = new JObject();
//                                        // 逐列讀取
//                                        for (int i = 0; i < receiveSQLdata.FieldCount; i++)
//                                        {
//                                            string columnName = receiveSQLdata.GetName(i);
//                                            object columnValue = receiveSQLdata.GetValue(i);
//                                            row[columnName] = JToken.FromObject(columnValue);
//                                        }
//                                        // 將行資料添加到 JArray 中
//                                        jsonArray.Add(row);
//                                    }
//                                    response.Data = jsonArray;
//                                }
//                            }
//                        }
//                        else
//                        {
//                            int affectedRows = SQLcmd.ExecuteNonQuery();
//                            long insertId = SQLcmd.LastInsertedId;  // 獲取自動生成的主鍵ID
//                            JObject resultObj = new JObject();  // 用於存放回傳結果
//                                                                // 設定回傳的 JSON 結果
//                            resultObj["fieldCount"] = 0; // MySQL 的插入操作 fieldCount 通常為 0
//                            resultObj["affectedRows"] = affectedRows;
//                            resultObj["insertId"] = insertId;
//                            resultObj["info"] = "";  // 可能為空
//                            resultObj["serverStatus"] = 2;  // 模擬結果值，通常 2 代表連接正常
//                            resultObj["warningStatus"] = 0;  // 假設沒有警告
//                            response.Data.Add(resultObj);
//                        }
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                //response.Error = ex.Message;
//                response.Error = $"{ex.Message}\n{", StackTrace:" + ex.StackTrace}";
//            }
//            return response;
//        }
//    }
//    public class MySqlConnectionManager
//    {
//        private static string connectionString;
//        public static string ConnectionString { get { return connectionString; } }
//        public static void Initialize(string IP, string Port, string user, string password, string dbName)
//        {
//            connectionString = $"server={IP};port={Port};user id={user};password={password};database={dbName};sslMode=none;Pooling=true;Min Pool Size=0;Max Pool Size=100;Connection Lifetime=0;";
//        }
//    }
//}