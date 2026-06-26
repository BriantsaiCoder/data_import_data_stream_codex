using System;
using System.Collections.Generic;
using System.IO;
using static DCT_data_import.DbObject;
namespace DCT_data_import
{
    public class DbAccess
    {
        public int SelectDataCountInDays(DatabaseService DatabaseService, int day, string mode = "tester")
        {
            WriteToLog writeToLog = new WriteToLog();
            // 檢查資料庫和相關資料表是否存在
            string tableName = mode == "tester" ? "db_key" : "db_key_ui_status";
            if (!DatabaseService.CheckDatabaseAndTableExists(tableName))
            {
                writeToLog.WriteErrorLog($"資料庫或資料表 {tableName} 不存在");
                return -1;
            }
            string sql = string.Empty;
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
                Execute_query execute_query = new Execute_query
                {
                    Query = sql
                };
                Execute_query_response response = DatabaseService.ExecuteSqlAsync(execute_query, "select").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteErrorLog($"SQL Query: {execute_query.Query}");
                    writeToLog.WriteErrorLog($"Error: {response.Error}");
                    writeToLog.WriteErrorLog("SELECT `db_key` error! ");
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
                writeToLog.WriteErrorLog("SelectDataCountInDays() error:" + ex.Message);
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
            // 檢查資料庫和相關資料表是否存在
            string tableName = mode == "tester" ? "db_key" : "db_key_ui_status";
            if (!DatabaseService.CheckDatabaseAndTableExists(tableName))
            {
                writeToLog.WriteErrorLog($"資料庫或資料表 {tableName} 不存在");
                return new List<DbKeyObject>();
            }
            string sql = string.Empty;
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
                Execute_query execute_query = new Execute_query
                {
                    Query = sql
                };
                Execute_query_response response = DatabaseService.ExecuteSqlAsync(execute_query, "select").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteErrorLog($"SQL Query: {execute_query.Query}");
                    writeToLog.WriteErrorLog($"Error: {response.Error}");
                    writeToLog.WriteErrorLog("SELECT `db_key` error! ");
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
                        // Safe parsing for all integer fields
                        if (!int.TryParse(response.Data[i]["id"]?.ToString(), out int id) ||
                            !int.TryParse(response.Data[i]["recovery_rate"]?.ToString(), out int recoveryRate) ||
                            !int.TryParse(response.Data[i]["tester"]?.ToString(), out int tester) ||
                            !int.TryParse(response.Data[i]["test_result"]?.ToString(), out int testResult) ||
                            !int.TryParse(response.Data[i]["fail_pin"]?.ToString(), out int failPin) ||
                            !int.TryParse(response.Data[i]["check_status"]?.ToString(), out int checkStatus))
                        {
                            writeToLog.WriteToDataImportLog($"SelectDbKey() invalid integer data at row {i}, skipping row");
                            continue;
                        }
                        dbKeyList.Add(new DbKeyObject(id, response.Data[i]["db_key"].ToString(), recoveryRate, tester, testResult, failPin, checkStatus));
                    }
                    else
                    {
                        // Safe parsing for id and check_status fields
                        if (!int.TryParse(response.Data[i]["id"]?.ToString(), out int id) ||
                            !int.TryParse(response.Data[i]["check_status"]?.ToString(), out int checkStatus))
                        {
                            writeToLog.WriteToDataImportLog($"SelectDbKey() invalid integer data at row {i}, skipping row");
                            continue;
                        }
                        dbKeyList.Add(new DbKeyObject(id, response.Data[i]["db_key"].ToString(), checkStatus));
                    }
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("SelectDbKey() error:" + ex.Message);
                Console.WriteLine(ex.ToString());
                return new List<DbKeyObject>();
            }
            return dbKeyList;
        }
        /// <summary>
        /// 由各匯入分量回傳碼組出與 <c>db_key.check_status</c> 比對用的 bitmask。
        /// </summary>
        /// <param name="recoveryRate">RecoveryRate 分量結果,佔 bit3(8)。</param>
        /// <param name="tester">Tester 分量結果,佔 bit2(4)。</param>
        /// <param name="testResult">RawData/TestResult 分量結果,佔 bit1(2)。</param>
        /// <param name="failPin">FailPin 分量結果,佔 bit0(1)。</param>
        /// <returns>加權和 <c>8*recoveryRate + 4*tester + 2*testResult + failPin</c>。</returns>
        /// <remarks>
        /// 公式假設每個分量只回 0/1。任一分量回 2/3(匯入函式實際值域)會使加權和溢位、污染高位 bit,
        /// 使 <c>importResult == check_status</c> 恆 false 而誤判失敗(見 docs/codebase/CONCERNS.md R5)。
        /// </remarks>
        public static int ComputeImportResult(int recoveryRate, int tester, int testResult, int failPin)
        {
            return 8 * recoveryRate + 4 * tester + 2 * testResult + failPin;
        }
        public string UpdateDbKeyImportStatus(DatabaseService DatabaseService, string dbKey, int recoveryRate, int tester, int testResult, int failPin, string remark)
        {
            WriteToLog writeToLog = new WriteToLog();
            // 檢查資料庫和 db_key 資料表是否存在
            if (!DatabaseService.CheckDatabaseAndTableExists("db_key"))
            {
                writeToLog.WriteErrorLog("資料庫或 db_key 資料表不存在");
                return "Fail. Database or table does not exist";
            }
            //int importResult = 4 * tester + 2 * testResult + failPin;
            int importResult = ComputeImportResult(recoveryRate, tester, testResult, failPin);
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
                    writeToLog.WriteErrorLog($"SQL Query: {execute_query.Query}");
                    writeToLog.WriteErrorLog($"Error: {response.Error}");
                    writeToLog.WriteErrorLog("SELECT `db_key` error! ");
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
                if (int.TryParse(checkStatus, out int checkStatusInt) && importResult == checkStatusInt)
                {
                    importStatus = "1"; // import successfully
                }
                else
                {
                    if (!int.TryParse(checkStatus, out checkStatusInt))
                    {
                        writeToLog.WriteErrorLog($"UpdateDbKey() invalid checkStatus value: {checkStatus}");
                    }
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
                    writeToLog.WriteErrorLog("UPDATE `db_key` error! ");
                    return "Fail. Execution 'update' error: " + response.Error;
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("UpdateDbKeyImportStatus() error:" + ex.Message);
                Console.WriteLine(ex.ToString());
                return "Fail. Exception error";
            }
            return "OK";
        }
        public string UpdateDbKeyUiStatusImportStatus(DatabaseService DatabaseService, string dbKey, int uiStatus, string remark)
        {
            WriteToLog writeToLog = new WriteToLog();
            // 檢查資料庫和 db_key_ui_status 資料表是否存在
            if (!DatabaseService.CheckDatabaseAndTableExists("db_key_ui_status"))
            {
                writeToLog.WriteErrorLog("資料庫或 db_key_ui_status 資料表不存在");
                return "Fail. Database or table does not exist";
            }
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
                    writeToLog.WriteErrorLog($"SQL Query: {execute_query.Query}");
                    writeToLog.WriteErrorLog($"Error: {response.Error}");
                    writeToLog.WriteErrorLog("SELECT `db_key` error! ");
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
                if (int.TryParse(checkStatus, out int checkStatusInt) && importResult == checkStatusInt)
                {
                    importStatus = "1";
                }
                else
                {
                    if (!int.TryParse(checkStatus, out checkStatusInt))
                    {
                        writeToLog.WriteErrorLog($"UpdateDbKeyUiStatus() invalid checkStatus value: {checkStatus}");
                    }
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
                    writeToLog.WriteErrorLog("UPDATE `db_key` error! ");
                    return "Fail. Execution 'update' error: " + response.Error;
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("UpdateDbKeyUiStatusImportStatus() error:" + ex.Message);
                Console.WriteLine(ex.ToString());
                return "Fail. Exception error";
            }
            return "OK";
        }
        public List<DbKeyObject> SelectFailDbKeyResult(DatabaseService DatabaseService, string mode = "")
        {
            List<DbKeyObject> dbKeyObject = new List<DbKeyObject>();
            WriteToLog writeToLog = new WriteToLog();
            // 檢查資料庫和相關資料表是否存在
            string tableName = mode == "tester" ? "db_key" : "db_key_ui_status";
            if (!DatabaseService.CheckDatabaseAndTableExists(tableName))
            {
                writeToLog.WriteErrorLog($"資料庫或資料表 {tableName} 不存在");
                return new List<DbKeyObject>();
            }
            string sql = string.Empty, remark = string.Empty;
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
                    writeToLog.WriteErrorLog($"SQL Query: {execute_query.Query}");
                    writeToLog.WriteErrorLog($"Error: {response.Error}");
                    writeToLog.WriteErrorLog("SELECT `db_key` error! ");
                }
                for (int i = 0; i < response.Data.Count; i++)
                {
                    //Console.WriteLine(response.data[i]["db_key"].ToString());
                    remark = (response.Data[i]["check_status"].ToString() == "0") ? "未更新check status" : response.Data[i]["remark"].ToString();
                    // Safe parsing for id field
                    if (!int.TryParse(response.Data[i]["id"]?.ToString(), out int id))
                    {
                        writeToLog.WriteInfoLog($"SelectFailDbKeyResult() invalid id value at row {i}: {response.Data[i]["id"]}, skipping row");
                        continue;
                    }
                    dbKeyObject.Add(new DbKeyObject(id, response.Data[i]["db_key"].ToString(), remark));
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("SelectFailDbKeyResult() error:" + ex.Message);
                Console.WriteLine(ex.ToString());
                return dbKeyObject;
            }
            return dbKeyObject;
        }
        public List<DbKeyObject> SelectFailDbKeyFromFile()
        {
            List<DbKeyObject> dbKeyObject = new List<DbKeyObject>();
            string log_path = Path.Combine(AppContext.BaseDirectory, "mail_temp.txt");
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