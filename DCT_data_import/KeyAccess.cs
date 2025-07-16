using System;
using static DCT_data_import.DbObject;
namespace DCT_data_import
{
    public class KeyAccess
    {
        private readonly DatabaseService _DatabaseService;
        public KeyAccess()
        {
            _DatabaseService = new DatabaseService();
        }
        public string InsertDbKey(string mode, string dbKey)
        {
            Execute_query_response response;
            string sql = "";
            long timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            try
            {
                if (mode == "tester")
                {
                    //sql = "INSERT INTO db_key (datetime, db_key) VALUES ('" + timeStamp.ToString() + "', '" + dbKey + "');";
                    sql = "INSERT INTO db_key (datetime, db_key, check_status) VALUES ('" + timeStamp.ToString() + "', '" + dbKey + "','2');";
                }
                else if (mode == "ui_status")
                {
                    sql = "INSERT INTO db_key_ui_status (datetime, db_key) VALUES ('" + timeStamp.ToString() + "', '" + dbKey + "');";
                }
                else
                {
                    return "FAIL. Please give the mode ['tester' or 'ui_status']";
                }
                bool isDbKeyExist = IsDbKeyExist(mode, dbKey);  // 已存在: true  不存在: false
                if (isDbKeyExist)
                {
                    return "FAIL. The db_key=" + dbKey + " already exists in the database";
                }
                // 宣告 Web API body
                Execute_query poolExcute = new Execute_query
                {
                    Query = sql
                };
                // 回傳 {"data":{"fieldCount":0,"affectedRows":1,"insertId":1,"info":"","serverStatus":2,"warningStatus":0},"error":null}
                response = _DatabaseService.ExecuteSqlAsync(poolExcute, "insert").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.Error))
                {
                    return "FAIL. " + response.Error;
                }
            }
            catch (Exception ex)
            {
                return "FAIL. " + ex.ToString();
            }
            return "OK. ";
        }
        public string UpdateCheckStatus(string mode, string dbKey, int checkStatus)
        {
            Execute_query_response response;
            string sql = "";
            try
            {
                if (mode == "tester")
                {
                    sql = "UPDATE  db_key SET check_status='" + checkStatus + "' WHERE db_key='" + dbKey + "';";
                }
                else if (mode == "ui_status")
                {
                    sql = "UPDATE  db_key_ui_status SET check_status='" + checkStatus + "' WHERE db_key='" + dbKey + "';";
                }
                else
                {
                    return "FAIL. Please give the mode ['tester' or 'ui_status']";
                }
                bool isDbKeyExist = IsDbKeyExist(mode, dbKey);  // 已存在: true  不存在: false
                if (!isDbKeyExist)
                {
                    return "FAIL. The db_key='" + dbKey + "' does not yet exist in the database. ";
                }
                // 確認有這個db_key後，執行update。
                Execute_query poolExcute = new Execute_query
                {
                    Query = sql
                };
                response = _DatabaseService.ExecuteSqlAsync(poolExcute, "update").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.Error))
                {
                    return "FAIL. " + response.Error;
                }
            }
            catch (Exception ex)
            {
                return "FAIL. " + ex.ToString();
            }
            return "OK. ";
        }
        public bool IsDbKeyExist(string mode, string dbKey)
        {
            Execute_query_response response;
            string queryDbKey = "";
            if (mode == "tester")
            {
                queryDbKey = "SELECT db_key FROM db_key WHERE db_key='" + dbKey + "'";
            }
            else if (mode == "ui_status")
            {
                queryDbKey = "SELECT db_key FROM db_key_ui_status WHERE db_key='" + dbKey + "'";
            }
            // 查詢是否有這個db_key
            Execute_query poolExcute = new Execute_query
            {
                Query = queryDbKey
            };
            response = _DatabaseService.ExecuteSqlAsync(poolExcute, "select").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.Error))
            {
                Console.WriteLine("IsDbKeyExist() execution error: " + response.Error);
                return true;
            }
            if (response.Data.Count < 1)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}