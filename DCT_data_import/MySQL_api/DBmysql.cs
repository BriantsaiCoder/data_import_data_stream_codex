using System;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using static DCT_data_import.ApiObject;
namespace DCT_data_import
{
    public class DBmysql
    {
        private static readonly object lockObj = new object();
        public void Connect(string IP, string Port, string user, string password, string dbName)
        {
            MySqlConnectionManager.Initialize(IP, Port, user, password, dbName);
        }
        public Pool_execute_response Excute_mysql_cmd(string cmd_string, string mode = "select")
        {
            Pool_execute_response response = new Pool_execute_response();
            response.Data = new JArray();
            try
            {
                // 使用新的連線物件
                using (MySqlConnection localConnection = new MySqlConnection(MySqlConnectionManager.ConnectionString))
                {
                    localConnection.Open();
                    MySqlCommand SQLcmd = new MySqlCommand(cmd_string, localConnection);
                    if (mode == "select")
                    {
                        lock (lockObj) // 加鎖，確保執行緒安全
                        {
                            using (MySqlDataReader receiveSQLdata = SQLcmd.ExecuteReader())
                            {
                                // 將資料存儲到 JArray
                                JArray jsonArray = new JArray();
                                while (receiveSQLdata.Read())
                                {
                                    JObject row = new JObject();
                                    // 逐列讀取
                                    for (int i = 0; i < receiveSQLdata.FieldCount; i++)
                                    {
                                        string columnName = receiveSQLdata.GetName(i);
                                        object columnValue = receiveSQLdata.GetValue(i);
                                        row[columnName] = JToken.FromObject(columnValue);
                                    }
                                    // 將行資料添加到 JArray 中
                                    jsonArray.Add(row);
                                }
                                response.Data = jsonArray;
                            }
                        }
                    }
                    else
                    {
                        int affectedRows = SQLcmd.ExecuteNonQuery();
                        long insertId = SQLcmd.LastInsertedId;  // 獲取自動生成的主鍵ID
                        JObject resultObj = new JObject();  // 用於存放回傳結果
                                                            // 設定回傳的 JSON 結果
                        resultObj["fieldCount"] = 0; // MySQL 的插入操作 fieldCount 通常為 0
                        resultObj["affectedRows"] = affectedRows;
                        resultObj["insertId"] = insertId;
                        resultObj["info"] = "";  // 可能為空
                        resultObj["serverStatus"] = 2;  // 模擬結果值，通常 2 代表連接正常
                        resultObj["warningStatus"] = 0;  // 假設沒有警告
                        response.Data.Add(resultObj);
                    }
                }
            }
            catch (Exception ex)
            {
                //response.Error = ex.Message;
                response.Error = $"{ex.Message}\n{", StackTrace:" + ex.StackTrace}";
            }
            return response;
        }
    }
    public class MySqlConnectionManager
    {
        private static string connectionString;
        public static string ConnectionString { get { return connectionString; } }
        public static void Initialize(string IP, string Port, string user, string password, string dbName)
        {
            connectionString = $"server={IP};port={Port};user id={user};password={password};database={dbName};sslMode=none;Pooling=true;Min Pool Size=0;Max Pool Size=100;Connection Lifetime=0;";
        }
    }
}