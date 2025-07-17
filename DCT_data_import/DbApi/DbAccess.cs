using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using static DCT_data_import.DbObject;
namespace DCT_data_import
{
    public class DbAccess
    {
        public int SelectDataCountInDays(DatabaseService DatabaseService, int day, string mode = "tester")
        {
            WriteToLog writeToLog = new WriteToLog();
            string sql = "";
            long nowTimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            long threeHourAgoTimeStamp = nowTimeStamp - 86400 * day;  // 24小時=86400秒
            int count = 0;
            if (mode == "tester")
            {
                sql = "SELECT COUNT(id) count_id FROM `db_key` WHERE datetime >= " + threeHourAgoTimeStamp;
            }
            else if (mode == "ui_status")
            {
                sql = "SELECT COUNT(id) count_id FROM `db_key_ui_status` WHERE  datetime >= " + threeHourAgoTimeStamp;
            }
            try
            {
                // 宣告 Web API body
                Execute_query execute_query = new Execute_query
                {
                    Query = sql
                };
                // 回傳 {"data":{"fieldCount":0,"affectedRows":1,"insertId":1,"info":"","serverStatus":2,"warningStatus":0},"error":null}
                Execute_query_response response = DatabaseService.ExecuteSqlAsync(execute_query, "select").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteToDataImportLog($"SQL Query: {execute_query.Query}");
                    writeToLog.WriteToDataImportLog($"Error: {response.Error}");
                    writeToLog.WriteToDataImportLog("SELECT `db_key` error! ");
                }
                if (int.TryParse(response.Data[0]["count_id"].ToString(), out count))
                {
                    return count;
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteToDataImportLog("SelectDataCountInDays() error:" + ex.Message);
                Console.WriteLine(ex.ToString());
                return -1;
            }
        }
        /// <summary>
        /// 透過db_key table 擷取尚未匯入資料的flag進行匯入
        /// </summary>
        /// <param name="DatabaseService "></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public List<DbKeyObject> SelectDbKey(DatabaseService DatabaseService, string mode = "")
        {
            List<DbKeyObject> dbKeyList = new List<DbKeyObject>();
            WriteToLog writeToLog = new WriteToLog();
            string sql = "";
            if (mode == "tester")
            {
                //sql = "SELECT id, db_key, tester, test_result, fail_pin, check_status FROM `db_key` WHERE `check_status`>0 AND `import_status` =0 AND mail=0;";
                sql = "SELECT id, db_key, recovery_rate, tester, test_result, fail_pin, check_status FROM `db_key` WHERE `check_status`>0 AND `import_status` =0 AND mail=0;";
            }
            else if (mode == "ui_status")
            {
                sql = "SELECT id, db_key, check_status FROM `db_key_ui_status` WHERE  `check_status`>0 AND `import_status` =0 AND mail=0;";
            }
            try
            {
                // 宣告 Web API body
                Execute_query execute_query = new Execute_query
                {
                    Query = sql
                };
                // 回傳 {"data":{"fieldCount":0,"affectedRows":1,"insertId":1,"info":"","serverStatus":2,"warningStatus":0},"error":null}
                Execute_query_response response = DatabaseService.ExecuteSqlAsync(execute_query, "select").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteToDataImportLog($"SQL Query: {execute_query.Query}");
                    writeToLog.WriteToDataImportLog($"Error: {response.Error}");
                    writeToLog.WriteToDataImportLog("SELECT `db_key` error! ");
                }
                for (int i = 0; i < response.Data.Count; i++)
                {
                    //Console.WriteLine("id = " + response.Data[i]["id"].ToString());
                    //Console.WriteLine("db_key = " + response.Data[i]["db_key"].ToString());
                    //Console.WriteLine("recovery_rate = " + response.Data[i]["recovery_rate"].ToString());
                    //Console.WriteLine("tester = " + response.Data[i]["tester"].ToString());
                    //Console.WriteLine("test_result = " + response.Data[i]["test_result"].ToString());
                    //Console.WriteLine("fail_pin = " + response.Data[i]["fail_pin"].ToString());
                    //Console.WriteLine("check_status = " + response.Data[i]["check_status"].ToString());
                    if (mode == "tester")
                    {
                        //dbKeyList.Add(new DbKeyObject(int.Parse(response.Data[i]["id"].ToString()), response.Data[i]["db_key"].ToString(), int.Parse(response.Data[i]["tester"].ToString()), int.Parse(response.Data[i]["test_result"].ToString()), int.Parse(response.Data[i]["fail_pin"].ToString()), int.Parse(response.Data[i]["check_status"].ToString())));
                        dbKeyList.Add(new DbKeyObject(int.Parse(response.Data[i]["id"].ToString()), response.Data[i]["db_key"].ToString(), int.Parse(response.Data[i]["recovery_rate"].ToString()), int.Parse(response.Data[i]["tester"].ToString()), int.Parse(response.Data[i]["test_result"].ToString()), int.Parse(response.Data[i]["fail_pin"].ToString()), int.Parse(response.Data[i]["check_status"].ToString())));
                    }
                    else
                    {
                        dbKeyList.Add(new DbKeyObject(int.Parse(response.Data[i]["id"].ToString()), response.Data[i]["db_key"].ToString(), int.Parse(response.Data[i]["check_status"].ToString())));
                    }
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteToDataImportLog("SelectDbKey() error:" + ex.Message);
                Console.WriteLine(ex.ToString());
                return new List<DbKeyObject>();
            }
            return dbKeyList;
        }
        public string UpdateDbKeyImportStatus(DatabaseService DatabaseService, string dbKey, int recoveryRate, int tester, int testResult, int failPin, string remark)
        {
            WriteToLog writeToLog = new WriteToLog();
            //int importResult = 4 * tester + 2 * testResult + failPin;
            int importResult = 8 * recoveryRate + 4 * tester + 2 * testResult + failPin;
            string id, checkStatus, importStatus = "1", mail = "0";
            try
            {
                // 先select 出check status 比對確認結果
                Execute_query execute_query = new Execute_query
                {
                    Query = "SELECT id, check_status FROM db_key WHERE db_key='" + dbKey + "';"
                };
                Execute_query_response response = DatabaseService.ExecuteSqlAsync(execute_query, "select").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteToDataImportLog($"SQL Query: {execute_query.Query}");
                    writeToLog.WriteToDataImportLog($"Error: {response.Error}");
                    writeToLog.WriteToDataImportLog("SELECT `db_key` error! ");
                    return "Fail. Execution 'select' error: " + response.Error;
                }
                if (response.Data.Count > 0)
                {
                    id = response.Data[0]["id"].ToString();
                    checkStatus = response.Data[0]["check_status"].ToString();
                }
                else
                {
                    return "Fail. There is no information which db_key is '" + dbKey + "'";
                }
                // 檢查確認碼
                //if (importResult < int.Parse(checkStatus))
                //{
                //    importStatus = "0";
                //}
                /*else */
                if (importResult == int.Parse(checkStatus))
                {
                    importStatus = "1"; // import successfully
                }
                else
                {
                    importStatus = "2"; // import fail
                    mail = "1";
                    // 寫入寄信暫存檔
                    writeToLog.WriteToMailTemp(dbKey + "," + remark);
                }
                //importStatus = (importResult.ToString() == checkStatus) ? "1" : "2";
                // 更新 import check 相關資訊
                execute_query = new Execute_query
                {
                    Query = "UPDATE db_key " + "SET recovery_rate=" + recoveryRate.ToString() +
                    ",tester=" + tester.ToString() + ",test_result=" + testResult.ToString() + ",fail_pin=" + failPin.ToString() + "," +
                    "import_status=" + importStatus + ",mail=" + mail + ",remark='" + remark + "' " +
                    "WHERE `db_key`='" + dbKey + "';"
                    //Query = "UPDATE db_key " +
                    //"SET tester=" + tester.ToString() + ",test_result=" + testResult.ToString() + ",fail_pin=" + failPin.ToString() + "," +
                    //"import_status=" + importStatus + ",mail=" + mail + ",remark='" + remark + "' " +
                    //"WHERE `db_key`='" + dbKey + "';"
                };
                response = DatabaseService.ExecuteSqlAsync(execute_query, "update").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteToDataImportLog("UPDATE `db_key` error! ");
                    return "Fail. Execution 'update' error: " + response.Error;
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteToDataImportLog("UpdateDbKeyImportStatus() error:" + ex.Message);
                Console.WriteLine(ex.ToString());
                return "Fail. Exception error";
            }
            return "OK";
        }
        public string UpdateDbKeyUiStatusImportStatus(DatabaseService DatabaseService, string dbKey, int uiStatus, string remark)
        {
            WriteToLog writeToLog = new WriteToLog();
            int importResult = uiStatus;
            string id, checkStatus, importStatus = "1", mail = "0";
            try
            {
                // 先select 出check status 比對確認結果
                Execute_query execute_query = new Execute_query
                {
                    Query = "SELECT id, check_status FROM db_key_ui_status WHERE db_key='" + dbKey + "';"
                };
                Execute_query_response response = DatabaseService.ExecuteSqlAsync(execute_query, "select").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteToDataImportLog($"SQL Query: {execute_query.Query}");
                    writeToLog.WriteToDataImportLog($"Error: {response.Error}");
                    writeToLog.WriteToDataImportLog("SELECT `db_key` error! ");
                    return "Fail. Execution 'select' error: " + response.Error;
                }
                if (response.Data.Count > 0)
                {
                    id = response.Data[0]["id"].ToString();
                    checkStatus = response.Data[0]["check_status"].ToString();
                }
                else
                {
                    return "Fail. There is no infomation which db_key is '" + dbKey + "'";
                }
                // 檢查確認碼
                //if (importResult < int.Parse(checkStatus))
                //{
                //    importStatus = "0";
                //}
                /*else*/
                if (importResult == int.Parse(checkStatus))
                {
                    importStatus = "1";
                }
                else
                {
                    importStatus = "2";
                    mail = "1";
                    // 寫入寄信暫存檔
                    writeToLog.WriteToMailTemp(dbKey + "," + remark);
                }
                //// 寫入寄信暫存檔
                //writeToLog.WriteToMailTemp(dbKey + "," + dbKey);
                // 更新 import check 相關資訊
                execute_query = new Execute_query
                {
                    Query = "UPDATE db_key_ui_status " +
                    "SET ui_status='" + uiStatus.ToString() + "', " +
                    "import_status='" + importStatus + "',mail='" + mail + "',remark='" + remark + "' " +
                    "WHERE `db_key`='" + dbKey + "';"
                };
                response = DatabaseService.ExecuteSqlAsync(execute_query, "update").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteToDataImportLog("UPDATE `db_key` error! ");
                    return "Fail. Execution 'update' error: " + response.Error;
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteToDataImportLog("UpdateDbKeyUiStatusImportStatus() error:" + ex.Message);
                Console.WriteLine(ex.ToString());
                return "Fail. Exception error";
            }
            return "OK";
        }
        public List<DbKeyObject> SelectFailDbKeyResult(DatabaseService DatabaseService, string mode = "")
        {
            List<DbKeyObject> dbKeyObject = new List<DbKeyObject>();
            WriteToLog writeToLog = new WriteToLog();
            string sql = "", remark = "";
            long nowTimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            //long threeHourAgoTimeStamp = nowTimeStamp - 10800;  // 3小時=10800秒  3小時前
            long threeHourAgoTimeStamp = nowTimeStamp - 1200;  // 20分鐘前
            //long threeHourAgoTimeStamp = nowTimeStamp + 10800;  // 3小時=10800秒  3小時後
            if (mode == "tester")
            {
                sql = @"SELECT id, db_key, check_status, remark FROM `db_key` WHERE `mail`=0 AND `import_status`=0 AND datetime <= " + threeHourAgoTimeStamp +
                          @" union ALL
                                SELECT id, db_key, check_status, remark FROM `db_key` WHERE `mail`= 0 AND `import_status`>=2 AND datetime <= " + threeHourAgoTimeStamp;
            }
            else if (mode == "ui_status")
            {
                sql = @"SELECT id, db_key, check_status, remark FROM `db_key_ui_status` WHERE `mail`=0 AND `import_status`=0 AND datetime <= " + threeHourAgoTimeStamp +
                          @" union ALL
                                SELECT id, db_key, check_status, remark FROM `db_key_ui_status` WHERE `mail`= 0 AND `import_status` >=2 AND datetime <= " + threeHourAgoTimeStamp;
            }
            try
            {
                Execute_query execute_query = new Execute_query
                {
                    Query = sql
                };
                Execute_query_response response = DatabaseService.ExecuteSqlAsync(execute_query, "select").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteToDataImportLog($"SQL Query: {execute_query.Query}");
                    writeToLog.WriteToDataImportLog($"Error: {response.Error}");
                    writeToLog.WriteToDataImportLog("SELECT `db_key` error! ");
                }
                for (int i = 0; i < response.Data.Count; i++)
                {
                    //Console.WriteLine(response.data[i]["db_key"].ToString());
                    remark = (response.Data[i]["check_status"].ToString() == "0") ? "未更新check status" : response.Data[i]["remark"].ToString();
                    dbKeyObject.Add(new DbKeyObject(int.Parse(response.Data[i]["id"].ToString()), response.Data[i]["db_key"].ToString(), remark));
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteToDataImportLog("SelectFailDbKeyResult() error:" + ex.Message);
                Console.WriteLine(ex.ToString());
                return dbKeyObject;
            }
            return dbKeyObject;
        }
        public List<DbKeyObject> SelectFailDbKeyFromFile()
        {
            List<DbKeyObject> dbKeyObject = new List<DbKeyObject>();
            string log_path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + @"\mail_temp.txt").LocalPath;
            if (File.Exists(log_path))
            {
                using (StreamReader reader = new StreamReader(log_path))
                {
                    string line = reader.ReadLine();
                    while (line != null)
                    {
                        string[] strSplit = line.Split(',');
                        if (strSplit.Length > 1)
                        {
                            dbKeyObject.Add(new DbKeyObject(strSplit[0], strSplit[1]));
                        }
                        line = reader.ReadLine();
                    }
                }
            }
            return dbKeyObject;
        }
    }
}