using System;
using System.Collections.Generic;
using System.IO;
using DCT_data_import.Common;
using static DCT_data_import.DbApi.DbObject;
namespace DCT_data_import.DbApi
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
            long nowTimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            long threeHourAgoTimeStamp = nowTimeStamp - 86400 * day;  // 24小時=86400秒
            int count = 0;
            try
            {
                DbSqlRequest sqlRequest = BuildDataCountInDaysQuery(mode, threeHourAgoTimeStamp);
                DbQueryResult response = DatabaseService.ExecuteQuery(sqlRequest);
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteErrorLog($"SQL Query: {sqlRequest.Query}");
                    writeToLog.WriteErrorLog($"Error: {response.Error}");
                    writeToLog.WriteErrorLog($"SELECT `{tableName}` error! ");
                    return -1;
                }
                if (response.Data == null || response.Data.Count == 0)
                {
                    writeToLog.WriteErrorLog($"SELECT `{tableName}` count returned no rows");
                    return -1;
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

        internal static DbSqlRequest BuildDataCountInDaysQuery(string mode, long threshold)
        {
            string sql;
            if (mode == "tester")
            {
                sql = "SELECT COUNT(id) count_id FROM `db_key` WHERE datetime >= @threshold";
            }
            else if (mode == "ui_status")
            {
                sql = "SELECT COUNT(id) count_id FROM `db_key_ui_status` WHERE  datetime >= @threshold";
            }
            else
            {
                throw new ArgumentException("Unsupported db_key mode", nameof(mode));
            }

            return new DbSqlRequest
            {
                Query = sql,
                Parameters = new { threshold }
            };
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
                DbSqlRequest sqlRequest = new DbSqlRequest
                {
                    Query = sql
                };
                DbQueryResult response = DatabaseService.ExecuteQuery(sqlRequest);
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteErrorLog($"SQL Query: {sqlRequest.Query}");
                    writeToLog.WriteErrorLog($"Error: {response.Error}");
                    writeToLog.WriteErrorLog($"SELECT `{tableName}` error! ");
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
                            writeToLog.WriteInfoLog($"SelectDbKey() invalid integer data at row {i}, skipping row");
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
                            writeToLog.WriteInfoLog($"SelectDbKey() invalid integer data at row {i}, skipping row");
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
        /// 由各匯入分量回傳碼組出與 <c>db_key.check_status</c> 比對用的 4-bit bitmask。
        /// </summary>
        /// <param name="recoveryRate">RecoveryRate 分量回傳碼,成功(1)時佔 bit3(8)。</param>
        /// <param name="tester">Tester 分量回傳碼,成功(1)時佔 bit2(4)。</param>
        /// <param name="testResult">RawData/TestResult 分量回傳碼,成功(1)時佔 bit1(2)。</param>
        /// <param name="failPin">FailPin 分量回傳碼,成功(1)時佔 bit0(1)。</param>
        /// <returns>各分量「是否成功」的 bitmask,恆落在 0..15。</returns>
        /// <remarks>
        /// 分量回傳碼值域為 0/1/2/3(<c>ImportResult.Result</c>:0=檔案不存在、1=成功、2=驗證/讀檔失敗、3=重複或匯入失敗),
        /// 唯有成功(1)代表該檢查通過、應設對應 bit;其餘(含失敗碼 2/3)一律視為未設位(0)。
        /// 故先把每個分量正規化為 0/1 再加權,避免失敗碼直接進加權和而溢位、污染高位 bit,
        /// 使 <c>importResult == check_status</c> 誤判失敗+寄信(見 docs/codebase/CONCERNS.md R5)。
        /// </remarks>
        public static int ComputeImportResult(int recoveryRate, int tester, int testResult, int failPin)
        {
            // 單點 root-cause guard——分量只認「成功==1」才設位,失敗碼 2/3 與缺席同視為 0。
            // 用 ==1?1:0 而非 Math.Min(x,1):後者會把失敗碼 2/3 也映成 1、反把失敗當成功(R5 pin #3 鎖定此差異)。
            return 8 * (recoveryRate == 1 ? 1 : 0)
                 + 4 * (tester == 1 ? 1 : 0)
                 + 2 * (testResult == 1 ? 1 : 0)
                 + (failPin == 1 ? 1 : 0);
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
            int importResult = ComputeImportResult(recoveryRate, tester, testResult, failPin);
            string id, checkStatus, importStatus = "1", mail = "0";
            try
            {
                // 先select 出check status 比對確認結果
                DbSqlRequest sqlRequest = BuildDbKeyStatusSelectQuery("db_key", dbKey);
                DbQueryResult selectResponse = DatabaseService.ExecuteQuery(sqlRequest);
                if (!string.IsNullOrEmpty(selectResponse.Error))
                {
                    writeToLog.WriteErrorLog($"SQL Query: {sqlRequest.Query}");
                    writeToLog.WriteErrorLog($"Error: {selectResponse.Error}");
                    writeToLog.WriteErrorLog("SELECT `db_key` error! ");
                    return "Fail. Execution 'select' error: " + selectResponse.Error;
                }
                if (selectResponse.Data.Count > 0)
                {
                    id = selectResponse.Data[0]["id"].ToString();
                    checkStatus = selectResponse.Data[0]["check_status"].ToString();
                }
                else
                {
                    return "Fail. There is no information which db_key is '" + dbKey + "'";
                }
                // 檢查確認碼
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
                // 更新 import check 相關資訊
                sqlRequest = BuildDbKeyImportStatusUpdateQuery(dbKey, recoveryRate, tester, testResult, failPin, remark, importStatus, mail);
                DbCommandResult response = DatabaseService.ExecuteCommand(sqlRequest);
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
                DbSqlRequest sqlRequest = BuildDbKeyStatusSelectQuery("db_key_ui_status", dbKey);
                DbQueryResult selectResponse = DatabaseService.ExecuteQuery(sqlRequest);
                if (!string.IsNullOrEmpty(selectResponse.Error))
                {
                    writeToLog.WriteErrorLog($"SQL Query: {sqlRequest.Query}");
                    writeToLog.WriteErrorLog($"Error: {selectResponse.Error}");
                    writeToLog.WriteErrorLog("SELECT `db_key_ui_status` error! ");
                    return "Fail. Execution 'select' error: " + selectResponse.Error;
                }
                if (selectResponse.Data.Count > 0)
                {
                    id = selectResponse.Data[0]["id"].ToString();
                    checkStatus = selectResponse.Data[0]["check_status"].ToString();
                }
                else
                {
                    return "Fail. There is no infomation which db_key is '" + dbKey + "'";
                }
                // 檢查確認碼
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
                // 更新 import check 相關資訊
                sqlRequest = BuildDbKeyUiStatusImportStatusUpdateQuery(dbKey, uiStatus, remark, importStatus, mail);
                DbCommandResult response = DatabaseService.ExecuteCommand(sqlRequest);
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteErrorLog("UPDATE `db_key_ui_status` error! ");
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

        internal static DbSqlRequest BuildDbKeyStatusSelectQuery(string tableName, string dbKey)
        {
            if (tableName != "db_key" && tableName != "db_key_ui_status")
            {
                throw new ArgumentException("Unsupported db_key table", nameof(tableName));
            }

            return new DbSqlRequest
            {
                Query = "SELECT id, check_status FROM `" + tableName + "` WHERE db_key=@dbKey;",
                Parameters = new { dbKey }
            };
        }

        internal static DbSqlRequest BuildDbKeyImportStatusUpdateQuery(
            string dbKey,
            int recoveryRate,
            int tester,
            int testResult,
            int failPin,
            string remark,
            string importStatus,
            string mail)
        {
            return new DbSqlRequest
            {
                Query = "UPDATE db_key SET recovery_rate=@recoveryRate,tester=@tester,test_result=@testResult,fail_pin=@failPin," +
                        "import_status=@importStatus,mail=@mail,remark=@remark WHERE `db_key`=@dbKey;",
                Parameters = new { recoveryRate, tester, testResult, failPin, importStatus, mail, remark, dbKey }
            };
        }

        internal static DbSqlRequest BuildDbKeyUiStatusImportStatusUpdateQuery(
            string dbKey,
            int uiStatus,
            string remark,
            string importStatus,
            string mail)
        {
            return new DbSqlRequest
            {
                Query = "UPDATE db_key_ui_status SET ui_status=@uiStatus,import_status=@importStatus,mail=@mail,remark=@remark WHERE `db_key`=@dbKey;",
                Parameters = new { uiStatus, importStatus, mail, remark, dbKey }
            };
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
