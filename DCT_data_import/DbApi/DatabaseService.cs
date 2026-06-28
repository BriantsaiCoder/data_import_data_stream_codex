using System;
using static DCT_data_import.DbObject;
namespace DCT_data_import
{
    public class DatabaseService
    {
        public DatabaseService()
        {
        }

        /// <summary>
        /// 執行資料庫查詢操作
        /// </summary>
        /// <param name="Execute_query">查詢物件</param>
        /// <param name="mode">操作模式，預設為 "select"</param>
        /// <returns>查詢結果</returns>
        public Execute_query_response ExecuteSql(Execute_query Execute_query, string mode = "select")
        {
            // 輸入驗證 - 完全相同於原始程式碼
            if (Execute_query == null)
            {
                return new Execute_query_response { Error = "查詢物件不能為空" };
            }
            if (string.IsNullOrWhiteSpace(Execute_query.Query))
            {
                return new Execute_query_response { Error = "查詢指令不能為空" };
            }
            // 驗證連線參數 - 完全相同於原始程式碼
            string server = Program.HOST;
            string user = Program.USER;
            string password = Program.PASSWORD;
            string port = Program.PORT;
            string database = Program.DATABASE;
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(database))
            {
                return new Execute_query_response { Error = "資料庫連線參數不完整" };
            }
            try
            {
                var DB = new DBmysql();
                DB.Connect(server, port, user, password, database);
                return DB.Excute_mysql_cmd(Execute_query.Query, mode, Execute_query.Parameters);
            }
            catch (Exception ex)
            {
                var writeToLog = new WriteToLog();
                writeToLog.WriteErrorLog($"[DatabaseService.ExecuteSql] 資料庫操作失敗: {ex.Message}");
                Console.WriteLine($"[DatabaseService] 資料庫操作失敗: {ex.Message}");
                return new Execute_query_response { Error = GetSafeErrorMessage(ex) };
            }
        }
        /// <summary>
        /// 驗證資料庫連線是否正常
        /// </summary>
        /// <returns>連線是否成功</returns>
        public bool TestConnection()
        {
            try
            {
                string server = Program.HOST;
                string user = Program.USER;
                string password = Program.PASSWORD;
                string port = Program.PORT;
                string database = Program.DATABASE;
                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(user) ||
                    string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(database))
                {
                    return false;
                }
                var DB = new DBmysql();
                DB.Connect(server, port, user, password, database);
                var testResult = DB.Excute_mysql_cmd("SELECT 1 as test", "select");
                return string.IsNullOrEmpty(testResult.Error);
            }
            catch (Exception ex)
            {
                var writeToLog = new WriteToLog();
                writeToLog.WriteErrorLog($"[DatabaseService.TestConnection] 測試連線失敗: {ex.Message}");
                Console.WriteLine($"[DatabaseService] 測試連線失敗: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 檢查資料庫和資料表是否都存在（僅檢查，不創建）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>true: 都存在, false: 有一個不存在</returns>
        public bool CheckDatabaseAndTableExists(string tableName)
        {
            try
            {
                // 檢查資料庫
                var dbResult = ExecuteSql(BuildDatabaseExistsQuery(Program.DATABASE), "select");

                if (!string.IsNullOrEmpty(dbResult.Error) || dbResult.Data == null || dbResult.Data.Count == 0)
                    return false;

                // 檢查資料表
                var tableResult = ExecuteSql(BuildTableExistsQuery(Program.DATABASE, tableName), "select");

                return string.IsNullOrEmpty(tableResult.Error) && tableResult.Data != null && tableResult.Data.Count > 0;
            }
            catch (Exception ex)
            {
                var writeToLog = new WriteToLog();
                writeToLog.WriteErrorLog($"[DatabaseService.CheckDatabaseAndTableExists] 檢查資料庫/資料表存在性失敗: {ex.Message}");
                Console.WriteLine($"[DatabaseService] 檢查資料庫/資料表存在性失敗: {ex.Message}");
                return false;
            }
        }

        internal static Execute_query BuildDatabaseExistsQuery(string databaseName)
        {
            return new Execute_query
            {
                Query = "SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = @databaseName",
                Parameters = new { databaseName }
            };
        }

        internal static Execute_query BuildTableExistsQuery(string databaseName, string tableName)
        {
            return new Execute_query
            {
                Query = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @databaseName AND TABLE_NAME = @tableName",
                Parameters = new { databaseName, tableName }
            };
        }

        /// <summary>
        /// 取得安全的錯誤訊息，只包含例外訊息，不包含 StackTrace
        /// </summary>
        /// <param name="ex">例外物件</param>
        /// <returns>安全的錯誤訊息</returns>
        private string GetSafeErrorMessage(Exception ex)
        {
            if (ex == null)
                return "未知錯誤";
            // 使用 ex.Message 取得錯誤訊息（不包含 StackTrace）
            string message = ex.Message;
            // 如果有內部例外，也包含其訊息
            if (ex.InnerException != null)
            {
                message += string.Format(" 內部錯誤: {0}", ex.InnerException.Message);
            }
            return string.Format("執行資料庫查詢時發生錯誤: {0}", message);
        }
    }
}
