using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DCT_data_import.ApiObject;

namespace DCT_data_import
{
    public class KeyAccess
    {
        public static string POOL_NAME = "DB_program";
        public static string HOST = "192.168.0.105";
        public static string PORT = "3308";
        public static string USER = "5910";
        public static string PASSWORD = "5910";
        public static string DATABASE = "dct_test";
        

        //private string POOL_NAME = ConfigurationManager.ConnectionStrings["PoolName"].ConnectionString;
        //private string HOST = ConfigurationManager.ConnectionStrings["Host"].ConnectionString;
        //private string PORT = ConfigurationManager.ConnectionStrings["Port"].ConnectionString;
        //private string USER = ConfigurationManager.ConnectionStrings["User"].ConnectionString;
        //private string PASSWORD = ConfigurationManager.ConnectionStrings["Password"].ConnectionString;
        //private string DATABASE = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
        private WebApiClient webApiClient;

        public KeyAccess()
        {
            webApiClient = new WebApiClient();
        }

        public string InsertDbKey(string mode, string dbKey)
        {
            Pool_excute_response response;
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
                Pool_excute poolExcute = new Pool_excute
                {
                    pool = POOL_NAME,
                    query = sql
                };
                // 回傳 {"data":{"fieldCount":0,"affectedRows":1,"insertId":1,"info":"","serverStatus":2,"warningStatus":0},"error":null}
                response = webApiClient.ExcutePoolAsync(poolExcute, "insert").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.error))
                {
                    return "FAIL. " + response.error;
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
            Pool_excute_response response;
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
                Pool_excute poolExcute = new Pool_excute
                {
                    pool = POOL_NAME,
                    query = sql
                };
                response = webApiClient.ExcutePoolAsync(poolExcute, "update").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.error))
                {
                    return "FAIL. " + response.error;
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
            Pool_excute_response response;
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
            Pool_excute poolExcute = new Pool_excute
            {
                pool = POOL_NAME,
                query = queryDbKey
            };
            response = webApiClient.ExcutePoolAsync(poolExcute, "select").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                Console.WriteLine("IsDbKeyExist() execution error: " + response.error);
                return true;
            }

            if (response.data.Count < 1)
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
