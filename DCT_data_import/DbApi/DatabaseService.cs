using System;
using System.Threading.Tasks;
using static DCT_data_import.DbObject;
namespace DCT_data_import
{
    public class DatabaseService
    {
        public DatabaseService()
        {
        }

        /// <summary>
        /// 執行資料庫查詢操作（保持原有 API 完全相容）
        /// </summary>
        /// <param name="Execute_query">查詢物件</param>
        /// <param name="mode">操作模式，預設為 "select"</param>
        /// <returns>查詢結果</returns>
        public async Task<Execute_query_response> ExecuteSqlAsync(Execute_query Execute_query, string mode = "select")
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
                // 使用 Task.Run 來真正實現非同步操作 - 完全相同於原始程式碼
                return await Task.Run(() =>
                {
                    var DB = new DBmysql();
                    DB.Connect(server, port, user, password, database);
                    return DB.Excute_mysql_cmd(Execute_query.Query, mode);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 保留完整錯誤訊息以便除錯，只移除 StackTrace 部分 - 完全相同於原始程式碼
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
            catch
            {
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
                var dbQuery = $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{Program.DATABASE}'";
                var dbResult = ExecuteSqlAsync(new Execute_query { Query = dbQuery }, "select").GetAwaiter().GetResult();

                if (!string.IsNullOrEmpty(dbResult.Error) || dbResult.Data == null || dbResult.Data.Count == 0)
                    return false;

                // 檢查資料表
                var tableQuery = $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{Program.DATABASE}' AND TABLE_NAME = '{tableName}'";
                var tableResult = ExecuteSqlAsync(new Execute_query { Query = tableQuery }, "select").GetAwaiter().GetResult();

                return string.IsNullOrEmpty(tableResult.Error) && tableResult.Data != null && tableResult.Data.Count > 0;
            }
            catch
            {
                return false;
            }
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
