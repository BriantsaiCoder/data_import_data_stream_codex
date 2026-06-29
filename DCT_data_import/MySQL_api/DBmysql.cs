using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using static DCT_data_import.DbObject;
namespace DCT_data_import
{
    public class DBmysql
    {
        public void Connect(string IP, string Port, string user, string password, string dbName)
        {
            MySqlConnectionManager.Initialize(IP, Port, user, password, dbName);
        }
        /// <summary>
        /// 過濾 SQL 命令字串，處理可能導致語法錯誤的雙引號模式
        /// </summary>
        /// <param name="cmd_string">原始 SQL 命令字串</param>
        /// <returns>過濾後的 SQL 命令字串</returns>
        private string FilterSqlCommand(string cmd_string)
        {
            if (string.IsNullOrWhiteSpace(cmd_string))
                return cmd_string;
            string originalCommand = cmd_string;
            string filteredCommand = cmd_string;
            // 先處理四個連續引號包圍的內容 """"content"""" 為單引號包圍 "content"
            filteredCommand = System.Text.RegularExpressions.Regex.Replace(
                filteredCommand,
                @"""""([^""""]*)""""",    // 匹配 """"任何內容""""
                @"""$1""",                   // 替換為 "內容"
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            // 然後處理兩個引號的情況 ""content"" -> "content" (雖然這個通常不是問題，但為了完整性)
            filteredCommand = System.Text.RegularExpressions.Regex.Replace(
                filteredCommand,
                @"""([^""]*)""",          // 匹配 ""任何內容""
                @"""$1""",                   // 替換為 "內容"
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            // 如果有改變，記錄日誌以便追蹤
            if (originalCommand != filteredCommand)
            {
                WriteToLog writeToLog = new WriteToLog();
                writeToLog.WriteInfoLog($"SQL 命令已過濾雙引號模式");
                writeToLog.WriteInfoLog($"原始: {originalCommand}");
                writeToLog.WriteInfoLog($"過濾後: {filteredCommand}");
            }
            return filteredCommand;
        }
        public DbQueryResult ExecuteQuery(string cmd_string, object parameters = null)
        {
            DbQueryResult response = new DbQueryResult();
            // 輸入驗證
            if (string.IsNullOrWhiteSpace(cmd_string))
            {
                response.Error = "SQL 指令不能為空";
                return response;
            }
            if (string.IsNullOrEmpty(MySqlConnectionManager.ConnectionString))
            {
                response.Error = "資料庫連線尚未初始化，請先呼叫 Connect 方法";
                return response;
            }
            // 過濾 SQL 命令以避免語法錯誤
            string filteredCmdString = FilterSqlCommand(cmd_string);
            MySqlConnection connection = null;
            try
            {
                connection = new MySqlConnection(MySqlConnectionManager.ConnectionString);
                connection.Open();
                ExecuteSelectCommand(connection, filteredCmdString, parameters, response);
            }
            catch (Exception ex)
            {
                response.Error = FormatDatabaseError(ex);
            }
            finally
            {
                // 確保連線正確釋放
                connection?.Close();
                connection?.Dispose();
            }
            return response;
        }
        public DbCommandResult ExecuteCommand(string cmd_string, object parameters = null)
        {
            DbCommandResult response = new DbCommandResult();
            // 輸入驗證
            if (string.IsNullOrWhiteSpace(cmd_string))
            {
                response.Error = "SQL 指令不能為空";
                return response;
            }
            // DryRun(影子驗證):寫入一律不寫入,回 no-op 成功(Error 空、AffectedRows/InsertId 為 0)。
            // gate 放在開連線前 → 影子模式零寫入連線負擔;涵蓋 INSERT/UPDATE/DELETE(含破壞性多表 cascade delete)。
            if (RuntimeMode.IsDryRun)
            {
                response.Error = string.Empty;
                return response;
            }
            if (string.IsNullOrEmpty(MySqlConnectionManager.ConnectionString))
            {
                response.Error = "資料庫連線尚未初始化，請先呼叫 Connect 方法";
                return response;
            }
            // 過濾 SQL 命令以避免語法錯誤
            string filteredCmdString = FilterSqlCommand(cmd_string);
            MySqlConnection connection = null;
            try
            {
                connection = new MySqlConnection(MySqlConnectionManager.ConnectionString);
                connection.Open();
                response = ExecuteNonQueryCommand(connection, filteredCmdString, parameters);
            }
            catch (Exception ex)
            {
                response.Error = FormatDatabaseError(ex);
            }
            finally
            {
                // 確保連線正確釋放
                connection?.Close();
                connection?.Dispose();
            }
            return response;
        }
        private void ExecuteSelectCommand(MySqlConnection connection, string cmd_string, object parameters, DbQueryResult response)
        {
            try
            {
                WriteToLog writeToLog = new WriteToLog();
                var results = connection.Query(cmd_string, parameters);
                JArray jsonArray = new JArray();
                foreach (var row in results)
                {
                    try
                    {
                        JObject jsonRow = new JObject();
                        var rowDict = row as IDictionary<string, object>;
                        if (rowDict != null)
                        {
                            foreach (var kvp in rowDict)
                            {
                                try
                                {
                                    // 安全的 JSON 轉換
                                    object value = kvp.Value ?? DBNull.Value;
                                    // 處理特殊資料類型
                                    if (value is DateTime dateTime)
                                    {
                                        jsonRow[kvp.Key] = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                                    }
                                    else if (value is decimal || value is double || value is float)
                                    {
                                        jsonRow[kvp.Key] = JToken.FromObject(value);
                                    }
                                    else if (value == DBNull.Value)
                                    {
                                        jsonRow[kvp.Key] = null;
                                    }
                                    else
                                    {
                                        jsonRow[kvp.Key] = JToken.FromObject(value);
                                    }
                                }
                                catch (Exception fieldEx)
                                {
                                    // 個別欄位轉換失敗時，記錄欄位名稱並設為 null
                                    jsonRow[kvp.Key] = null;
                                    // 可選：記錄警告日誌
                                    System.Diagnostics.Debug.WriteLine($"欄位 {kvp.Key} 轉換失敗: {fieldEx.Message}");
                                    writeToLog.WriteErrorLog($"cmd_string {cmd_string} 欄位 {kvp.Key} 轉換失敗: {fieldEx.Message}");
                                }
                            }
                        }
                        jsonArray.Add(jsonRow);
                    }
                    catch (Exception rowEx)
                    {
                        throw new InvalidOperationException($"處理查詢結果第 {jsonArray.Count + 1} 列時發生錯誤: {rowEx.Message}", rowEx);
                    }
                }
                response.Data = jsonArray;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"執行 SELECT 查詢時發生錯誤: {ex.Message}", ex);
            }
        }
        private DbCommandResult ExecuteNonQueryCommand(MySqlConnection connection, string cmd_string, object parameters)
        {
            MySqlTransaction transaction = null;
            try
            {
                WriteToLog writeToLog = new WriteToLog();
                transaction = connection.BeginTransaction();
                int affectedRows = connection.Execute(cmd_string, parameters, transaction);
                long insertId = 0;
                if (cmd_string.Trim().ToUpper().StartsWith("INSERT"))
                {
                    try
                    {
                        // 這裡使用 MySQL 的 LAST_INSERT_ID() 函數(讀取DB datatable中有 AUTO_INCREMENT 的欄位（通常是 id 欄位))
                        insertId = connection.QuerySingleOrDefault<long>("SELECT LAST_INSERT_ID()", transaction: transaction);
                    }
                    catch (Exception insertIdEx)
                    {
                        // LAST_INSERT_ID 取得失敗不應該影響主要操作
                        System.Diagnostics.Debug.WriteLine($"取得 LAST_INSERT_ID 失敗: {insertIdEx.Message}");
                        writeToLog.WriteErrorLog($"cmd_string {cmd_string} INSERT, 取得 LAST_INSERT_ID 失敗: {insertIdEx.Message}");
                        insertId = 0;
                    }
                }
                transaction.Commit();
                return new DbCommandResult
                {
                    AffectedRows = affectedRows,
                    InsertId = insertId,
                    Error = string.Empty
                };
            }
            catch (Exception ex)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch (Exception rollbackEx)
                {
                    // Safe string splitting with bounds checking
                    var cmdParts = cmd_string?.Split(' ');
                    string operationType = (cmdParts != null && cmdParts.Length > 0) ? cmdParts[0].ToUpper() : "UNKNOWN";
                    throw new InvalidOperationException($"執行 {operationType} 操作失敗，且回滾交易時也發生錯誤。原始錯誤: {ex.Message}，回滾錯誤: {rollbackEx.Message}", ex);
                }
                // Safe string splitting with bounds checking
                var cmdParts2 = cmd_string?.Split(' ');
                string operationType2 = (cmdParts2 != null && cmdParts2.Length > 0) ? cmdParts2[0].ToUpper() : "UNKNOWN";
                throw new InvalidOperationException($"執行 {operationType2} 操作失敗: {ex.Message}", ex);
            }
            finally
            {
                transaction?.Dispose();
            }
        }
        private string FormatDatabaseError(Exception ex)
        {
            if (ex is MySqlException mysqlEx)
            {
                return FormatMySqlError(mysqlEx);
            }
            if (ex is InvalidOperationException invalidOpEx)
            {
                return $"資料庫操作無效: {invalidOpEx.Message}";
            }
            if (ex is TimeoutException timeoutEx)
            {
                return $"資料庫操作逾時: {timeoutEx.Message}";
            }
            if (ex is ArgumentException argEx)
            {
                return $"參數錯誤: {argEx.Message}";
            }
            if (ex is InvalidCastException castEx)
            {
                return $"資料類型轉換錯誤: {castEx.Message}";
            }
            if (ex is Newtonsoft.Json.JsonException jsonEx)
            {
                return $"JSON 序列化錯誤: {jsonEx.Message}";
            }

            string error = $"執行資料庫操作時發生未預期錯誤: {ex.GetType().Name} - {ex.Message}";
            // 記錄詳細錯誤資訊用於除錯
            if (ex.InnerException != null)
            {
                error += $" | 內部例外: {ex.InnerException.Message}";
            }
            return error;
        }
        private string FormatMySqlError(MySqlException mysqlEx)
        {
            string errorMessage = $"MySQL 錯誤 (代碼: {mysqlEx.Number}): {mysqlEx.Message}";
            switch (mysqlEx.Number)
            {
                case 1062: errorMessage += " - 重複鍵值錯誤"; break;
                case 1452: errorMessage += " - 外鍵約束失敗"; break;
                case 1146: errorMessage += " - 資料表不存在"; break;
                case 1054: errorMessage += " - 欄位不存在"; break;
                case 1064: errorMessage += " - SQL 語法錯誤"; break;
                case 2006: errorMessage += " - MySQL 伺服器已斷線"; break;
                case 1045: errorMessage += " - 存取被拒絕，請檢查使用者名稱和密碼"; break;
                case 1049: errorMessage += " - 未知的資料庫"; break;
                case 1213: errorMessage += " - 交易死鎖"; break;
                case 1205: errorMessage += " - 鎖定等待逾時"; break;
                case 2013: errorMessage += " - 查詢期間與 MySQL 伺服器失去連線"; break;
                case 1040: errorMessage += " - 連線數過多"; break;
                case 1203: errorMessage += " - 使用者連線數超過限制"; break;
                default:
                    errorMessage += $" - 詳細錯誤: {mysqlEx.GetType().Name}";
                    if (mysqlEx.InnerException != null)
                    {
                        errorMessage += $" | 內部例外: {mysqlEx.InnerException.Message}";
                    }
                    break;
            }
            return errorMessage;
        }
    }
    public class MySqlConnectionManager
    {
        private static volatile string connectionString;
        private static readonly object lockObject = new object();
        public static string ConnectionString
        {
            get { return connectionString; }
        }
        public static void Initialize(string IP, string Port, string user, string password, string dbName)
        {
            // 輸入驗證
            if (string.IsNullOrWhiteSpace(IP)) throw new ArgumentException("IP 不能為空", nameof(IP));
            if (string.IsNullOrWhiteSpace(Port)) throw new ArgumentException("Port 不能為空", nameof(Port));
            if (string.IsNullOrWhiteSpace(user)) throw new ArgumentException("使用者名稱不能為空", nameof(user));
            if (string.IsNullOrWhiteSpace(dbName)) throw new ArgumentException("資料庫名稱不能為空", nameof(dbName));
            lock (lockObject)
            {
                if (string.IsNullOrEmpty(connectionString)) // 只初始化一次
                {
                    try
                    {
                        connectionString = $"server={IP};port={Port};user id={user};password={password};database={dbName};" +
                                          $"sslMode=Preferred;Pooling=true;Min Pool Size=5;Max Pool Size=100;" +
                                          $"Connection Lifetime=0;Connection Timeout=30;Command Timeout=60;" +
                                          $"Allow User Variables=true;Convert Zero Datetime=true;" +
                                          $"Charset=utf8mb4;";
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"建立連線字串時發生錯誤: {ex.Message}", ex);
                    }
                }
            }
        }
        // 新增：測試連線方法
        public static bool TestConnection()
        {
            if (string.IsNullOrEmpty(connectionString))
                return false;
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    return connection.State == ConnectionState.Open;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TestConnection() failed: {ex.Message}");
                return false;
            }
        }
        // 新增：重置連線字串方法
        public static void Reset()
        {
            lock (lockObject)
            {
                connectionString = null;
            }
        }
    }
}
