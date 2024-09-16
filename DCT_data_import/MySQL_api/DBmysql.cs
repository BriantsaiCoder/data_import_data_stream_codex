using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using static DCT_data_import.ApiObject;

namespace DCT_data_import
{
    public class DBmysql
    {
        string Mysql_host = string.Empty;

        private MySqlConnection mSQL;
        private MySqlCommand SQLcmd;
        private MySqlDataReader receiveSQLdata;

        public void connect(string IP, string Port, string user, string password, string dbName)
        {
            Mysql_host = $"server={IP};port={Port}; user id={user}; password={password}; database={dbName};sslMode=none";
            //Mysql_host = $"server={IP};port={Port}; user id={user}; password={password}; database={dbName}";

            mSQL = new MySqlConnection(Mysql_host);
            try
            {
                mSQL.Open();
                //Console.WriteLine($"{IP} Connect Successful !!!");

            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                switch (ex.Number)
                {
                    case 0:
                        Console.WriteLine("無法連線到資料庫.");
                        break;
                    case 1045:
                        Console.WriteLine("使用者帳號或密碼錯誤,請再試一次.");
                        break;
                    default:
                        Console.WriteLine("Connect Error : " + ex.Number);
                        break;
                }
            }
        }

        public void close()
        {
            try
            {
                mSQL.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error closing connection: " + ex.Message);
            }

        }

        public Pool_excute_response excute_mysql_cmd(string cmd_string,  string mode="select")
        {
            Pool_excute_response response = new Pool_excute_response();
            response.data = new JArray();

            try
            {
                SQLcmd = new MySqlCommand(cmd_string, mSQL);
                
                if(mode== "select")
                {

                    SQLcmd.ExecuteNonQuery();
                    using (MySqlDataReader receiveSQLdata = SQLcmd.ExecuteReader())
                    {
                        // 将数据存储到 JArray
                        JArray jsonArray = new JArray();

                        while (receiveSQLdata.Read())
                        {
                            JObject row = new JObject();

                            // 逐列读取
                            for (int i = 0; i < receiveSQLdata.FieldCount; i++)
                            {
                                string columnName = receiveSQLdata.GetName(i);
                                object columnValue = receiveSQLdata.GetValue(i);
                                row[columnName] = JToken.FromObject(columnValue);
                            }

                            // 将行数据添加到 JArray 中
                            jsonArray.Add(row);
                        }

                        response.data = jsonArray;
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
                    resultObj["info"] = "";  // 可能为空
                    resultObj["serverStatus"] = 2;  // 模擬結果值，通常 2 代表连接正常
                    resultObj["warningStatus"] = 0;  // 假設沒有警告

                    response.data.Add(resultObj);
                }


                //receiveSQLdata.Close();
            }
            catch (Exception ex)
            {
                response.error = "Error : " + ex.StackTrace + "_" + ex.Message;
            }

            return response;
        }
    }

    //public class PoolExcuteResponse
    //{
    //    public JArray data { get; set; }
    //    public string error { get; set; }
    //}
}
