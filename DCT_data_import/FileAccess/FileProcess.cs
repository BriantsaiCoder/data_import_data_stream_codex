using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Dapper;
using static DCT_data_import.DbObject;
namespace DCT_data_import
{
    public class FileProcess
    {
        //public DatabaseService  DatabaseService ;
        private readonly WriteToLog writeToLog;
        public bool MultiSiteRawDataLotInfoIsImported { get; set; }
        public string MultiSiteRawDataLotInfoInsertId { get; set; }
        public FileProcess()
        {
            //DatabaseService  = new DatabaseService ();
            writeToLog = new WriteToLog();
        }
        public bool IsDBKeyExistInDB(string db_table_name, string db_key, DatabaseService DatabaseService)
        {
            // 檢查資料庫和資料表是否存在
            if (!DatabaseService.CheckDatabaseAndTableExists(db_table_name))
            {
                writeToLog.WriteErrorLog($"資料庫或資料表 {db_table_name} 不存在");
                return false;
            }
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var execute_query = BuildDbKeyExistsQuery(db_table_name, db_key);
            Execute_query_response response = DatabaseService.ExecuteSql(execute_query);
            if (!string.IsNullOrEmpty(response.Error))
            {
                writeToLog.WriteErrorLog($"SQL Query: {execute_query.Query}");
                writeToLog.WriteErrorLog($"Error: {response.Error}");
                return false;
            }
            int length = 0;
            if (response.Data != null)
            {
                length = response.Data.Count;
            }
            stopwatch.Stop();
            long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            return (length > 0);
        }
        // 解析日期格式 "Jun_06_2022_12_08_22"
        public string CustomizeDateTimeParser(string datetime)
        {
            string[] time_split = datetime.Split('_');
            if (time_split.Length != 6) return string.Empty;
            string newDatetimeStr = time_split[0] + " " + time_split[1] + " " + time_split[2] + " " + time_split[3] + ":" + time_split[4] + ":" + time_split[5];
            DateTime dateTime = new DateTime();
            if (DateTime.TryParse(newDatetimeStr, out dateTime))
            {
                return dateTime.ToString("yyyy-MM-dd hh:mm:ss");
            }
            // 日期格式解析失敗
            else
            {
                return DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");
            }
        }
        public string ValidateDateTime(string input)
        {
            string format = "yyyy-MM-dd HH:mm:ss";
            if (DateTime.TryParseExact(input, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
            {
                return result.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
        // Recovery Rate 匯入資料庫
        public bool ImportRecoveryData(RecoveryRateDataContentFormat content, DatabaseService DatabaseService)
        {
            if (content.LotInfo.Rows.Count < 1 || content.LotRecoveryRate.Rows.Count < 1) return false;
            #region insert recovery rate 的 data
            int cut_size = (content.FinalRecoveryRateTable.Rows.Count > 5000) ? 5000 : content.FinalRecoveryRateTable.Rows.Count;
            // assign 需要 insert 的 欄位名稱
            string columns = string.Empty, values = string.Empty;
            Execute_query_response response2;
            for (int i = 0; i < content.FinalRecoveryRateTable.Columns.Count; i++)
            {
                string column_name = content.FinalRecoveryRateTable.Columns[i].ColumnName.ToLower();
                //column_name = column_name.Split('(', ')')[0];
                // 欄位名稱調整
                if (column_name == "DB Key".ToLower()) column_name = "db_key";
                if (column_name == "OS Machine".ToLower()) column_name = "os_machine";
                if (column_name == "AO Lot".ToLower()) column_name = "ao_lot";
                if (column_name == "reTestPass".ToLower()) column_name = "re_test_pass";
                if (column_name == "FailPinCount".ToLower()) column_name = "fail_pin_count";
                if (column_name == "Recovery rate(%)".ToLower()) column_name = "recovery_rate";
                columns += "`" + column_name.Trim() + "`";
                if (i != content.FinalRecoveryRateTable.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            try
            {
                // 開始逐一insert recovery rate data
                var valuesBuilder = new StringBuilder();
                var parameters = new DynamicParameters();
                int parameterIndex = 0;
                for (int i = 0; i < content.FinalRecoveryRateTable.Rows.Count; i++)
                {
                    valuesBuilder.Append("(").Append(AddInsertParameter(parameters, ref parameterIndex, "recovery_rate", ConvertEmptyToDefaultString(content.FinalRecoveryRateTable.Rows[i][0].ToString().Trim()))).Append(",");
                    for (int j = 1; j < content.FinalRecoveryRateTable.Columns.Count; j++)
                    { // 處理日期格式
                        if (string.Equals(content.FinalRecoveryRateTable.Columns[j].ColumnName.ToLower(), "date", StringComparison.OrdinalIgnoreCase))
                        {
                            // 日期格式解析正確
                            content.FinalRecoveryRateTable.Rows[i][j] = ValidateDateTime(content.FinalRecoveryRateTable.Rows[i][j].ToString());
                        }
                        valuesBuilder.Append(AddInsertParameter(parameters, ref parameterIndex, "recovery_rate", ConvertEmptyToDefaultString(content.FinalRecoveryRateTable.Rows[i][j].ToString().Trim())));
                        if (j != content.FinalRecoveryRateTable.Columns.Count - 1)
                        {
                            valuesBuilder.Append(",");
                        }
                    }
                    // 每cut_size個row就匯入一次
                    if (i != 0 && i % cut_size == 0 && valuesBuilder.Length > 0)
                    {
                        valuesBuilder.Append(")");
                        values = valuesBuilder.ToString();
                        values = values.Substring(1, values.Length - 2);
                        response2 = ExecuteInsert(DatabaseService, "recovery_rate", columns, values, parameters);
                        if (!string.IsNullOrEmpty(response2.Error))
                        {
                            writeToLog.WriteErrorLog("'INSERT INTO recovery_rate' error:" + response2.Error);
                            return false;
                        }
                        valuesBuilder.Clear();
                        values = string.Empty;
                        parameters = new DynamicParameters();
                        parameterIndex = 0;
                    }
                    else if (i != content.FinalRecoveryRateTable.Rows.Count - 1)
                    {
                        valuesBuilder.Append("),");
                    }
                    else
                    {
                        valuesBuilder.Append(")");
                    }
                }
                values = valuesBuilder.ToString();
                if (!string.IsNullOrEmpty(values))
                {
                    values = values.Substring(1, values.Length - 2);
                    // 如果最後一個字元是')' 則移除
                    string last_str = values.Substring(values.Length - 1);
                    if (last_str == ")")
                    {
                        values = values.Substring(0, values.Length - 1);
                    }
                    response2 = ExecuteInsert(DatabaseService, "recovery_rate", columns, values, parameters);
                    if (!string.IsNullOrEmpty(response2.Error))
                    {
                        writeToLog.WriteErrorLog("'INSERT INTO recovery_rate' response error:" + response2.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("'INSERT INTO recovery_rate' error:" + ex.Message);
                Console.WriteLine("'INSERT INTO recovery_rate' error:" + ex.Message);
                return false;
            }
            #endregion
            return true;
        }
        // RawData 匯入資料庫
        public bool ImportRawData(RawDataContentFormat content, DatabaseService DatabaseService)
        {
            if (content.LotInfo.Rows.Count < 1 || content.LotStatistic.Tables.Count < 1) return false;
            // assign 需要 insert 的 欄位名稱 與 values
            string columns = string.Empty, values = string.Empty;
            Execute_query_response response2;
            #region insert raw data 的 info 表格
            var lotsInfoParameters = new DynamicParameters();
            int lotsInfoParameterIndex = 0;
            for (int i = 0; i < content.LotInfo.Columns.Count; i++)
            {
                string column_name = content.LotInfo.Columns[i].ColumnName.ToLower();
                column_name = column_name.Split('(', ')')[0];
                // 欄位名稱調整
                if (column_name == "bondingdiagram") column_name = "bonding_diagram";
                if (column_name == "pass without ocr".ToLower()) column_name = "pass_without_ocr";
                if (column_name == "open without ocr".ToLower()) column_name = "open_without_ocr";
                if (column_name == "short & others".ToLower()) column_name = "short_others";
                if (column_name == "pass without ocr_ppm".ToLower()) column_name = "pass_without_ocr_ppm";
                if (column_name == "open without ocr_ppm".ToLower()) column_name = "open_without_ocr_ppm";
                if (column_name == "short & others_ppm".ToLower()) column_name = "short_others_ppm";
                // 處理日期格式
                if (column_name == "start" || column_name == "stop")
                {
                    // 日期格式解析正確
                    content.LotInfo.Rows[0][i] = CustomizeDateTimeParser(content.LotInfo.Rows[0][i].ToString());
                }
                columns += "`" + column_name.Trim() + "`";
                values += AddInsertParameter(lotsInfoParameters, ref lotsInfoParameterIndex, "lots_info", ConvertEmptyToDefaultString(content.LotInfo.Rows[0][i].ToString().Trim()));
                if (i != content.LotInfo.Columns.Count - 1)
                {
                    columns += ",";
                    values += ",";
                }
            }
            try
            {
                response2 = ExecuteInsert(DatabaseService, "lots_info", columns, values, lotsInfoParameters);
                if (!string.IsNullOrEmpty(response2.Error))
                {
                    writeToLog.WriteErrorLog("'INSERT INTO lots_info' error:" + response2.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("'INSERT INTO lots_info' error:" + ex.Message);
                writeToLog.WriteErrorLog("'INSERT INTO lots_info' error:" + ex.Message);
                return false;
            }
            #endregion
            string lotId = string.Empty;
            try
            {
                // 取得當前 lot id 值
                lotId = response2.Data[0]["insertId"].ToString();
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("'取得當前 lot id 值 error:" + ex.Message);
                Console.WriteLine(ex.ToString());
                return false;
            }
            #region 驗證和轉換統計數據中的數字欄位
            try
            {
                //writeToLog.WriteInfoLog("開始驗證統計數據中的數字欄位");
                int validationCount = 0;
                int conversionCount = 0;
                // 定義需要數字驗證的統計欄位
                string[] statisticParametersToValidate = { "stdev", "avg", "cp", "cpk" };
                for (int i = 0; i < content.LotStatistic.Tables.Count; i++)
                {
                    if (content.LotStatistic.Tables[i].Rows.Count < 1) continue;
                    DataTable table = content.LotStatistic.Tables[i];
                    DataRow row = table.Rows[0]; // 統計表通常只有一行數據
                    // 取得項目名稱用於日誌記錄
                    string itemName = table.Columns.Contains("Item Name") ?
                                     (row["Item Name"]?.ToString() ?? string.Format("Table_{0}", i)) :
                                     string.Format("Table_{0}", i);
                    foreach (string paramName in statisticParametersToValidate)
                    {
                        if (table.Columns.Contains(paramName))
                        {
                            string originalValue = row[paramName]?.ToString() ?? "";
                            double validatedValue = ValidateAndConvertStatisticValue(originalValue, paramName, itemName);
                            // 如果值發生了變化，記錄轉換
                            if (originalValue != validatedValue.ToString())
                            {
                                conversionCount++;
                            }
                            row[paramName] = validatedValue;
                            validationCount++;
                        }
                    }
                }
                //writeToLog.WriteInfoLog(string.Format("統計數據驗證完成，共處理 {0} 個欄位，轉換 {1} 個非數字值", validationCount, conversionCount));
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog(string.Format("統計數據驗證過程發生錯誤：{0}", ex.Message));
                writeToLog.WriteErrorLog(string.Format("錯誤詳細信息：{0}", ex.StackTrace));
                return false;
            }
            #endregion
            #region insert raw data 的 statistic 表格
            columns = "`lot_id`,"; values = string.Empty;
            for (int i = 0; i < content.LotStatistic.Tables[0].Columns.Count; i++)
            {
                string column_name = content.LotStatistic.Tables[0].Columns[i].ColumnName.ToLower();
                column_name = column_name.Replace(" ", "_");
                //System.Text.RegularExpressions.Regex.Replace(column_name, " ", "_");
                if (column_name == "#_of_pass")
                {
                    column_name = "pass";
                }
                if (column_name == "#_of_fail")
                {
                    column_name = "fail";
                }
                columns += "`" + column_name.Trim() + "`";
                if (i != content.LotStatistic.Tables[0].Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert 統計值表
            int test_count = 0, cut_size = 0;
            int.TryParse(content.LotStatistic.Tables[0].Rows[0]["# of PASS"].ToString(), out test_count);
            int tableCount = content.LotStatistic.Tables.Count;
            cut_size = (test_count > 0 && test_count < 10000) ? 10000 / test_count : 1;
            int lotResultCount = content.LotResult.Rows.Count;
            Console.WriteLine("itemCount=" + tableCount + " lotResultCount= " + lotResultCount);
            try
            {
                var valuesBuilder = new StringBuilder();
                var lotsStatisticParameters = new DynamicParameters();
                int lotsStatisticParameterIndex = 0;
                for (int i = 0; i < content.LotStatistic.Tables.Count; i++)
                {
                    if (content.LotStatistic.Tables[i].Rows.Count < 1) continue;
                    valuesBuilder.Append("(").Append(AddInsertParameter(lotsStatisticParameters, ref lotsStatisticParameterIndex, "lots_statistic", ConvertEmptyToDefaultString(lotId))).Append(",");
                    valuesBuilder.Append(AddInsertParameters(lotsStatisticParameters, ref lotsStatisticParameterIndex, "lots_statistic", content.LotStatistic.Tables[i].Rows[0].ItemArray.Select(item => ConvertEmptyToDefaultString(item?.ToString()))));
                    // 每cut_size個row就匯入一次
                    if (i != 0 && i % cut_size == 0)
                    {
                        valuesBuilder.Append(")");
                        values = valuesBuilder.ToString();
                        values = values.Substring(1, values.Length - 2);
                        response2 = ExecuteInsert(DatabaseService, "lots_statistic", columns, values, lotsStatisticParameters);
                        if (!string.IsNullOrEmpty(response2.Error))
                        {
                            writeToLog.WriteErrorLog("'INSERT INTO lots_statistic' response error:" + response2.Error);
                            response2 = DeleteRawData(DatabaseService, lotId);
                            return false;
                        }
                        valuesBuilder.Clear();
                        values = string.Empty;
                        lotsStatisticParameters = new DynamicParameters();
                        lotsStatisticParameterIndex = 0;
                    }
                    else if (i != content.LotStatistic.Tables.Count - 1)
                    {
                        valuesBuilder.Append("),");
                    }
                    else
                    {
                        valuesBuilder.Append(")");
                    }
                }
                values = valuesBuilder.ToString();
                if (values.Length > 3)
                {
                    values = values.Substring(1, values.Length - 2);
                    response2 = ExecuteInsert(DatabaseService, "lots_statistic", columns, values, lotsStatisticParameters);
                    if (!string.IsNullOrEmpty(response2.Error))
                    {
                        writeToLog.WriteErrorLog("'INSERT INTO lots_statistic' response error:" + response2.Error);
                        response2 = DeleteRawData(DatabaseService, lotId);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("'INSERT INTO lots_statistic' error:" + ex.Message);
                response2 = DeleteRawData(DatabaseService, lotId);
                return false;
            }
            #endregion
            #region insert raw data 的 result 表格
            cut_size = (content.LotResult.Rows.Count > 5000) ? 5000 : content.LotResult.Rows.Count;
            columns = "`lot_id`,"; values = string.Empty;
            for (int i = 0; i < content.LotResult.Columns.Count; i++)
            {
                string column_name = content.LotResult.Columns[i].ColumnName.ToLower();
                column_name = column_name.Replace(" ", "_");
                if (column_name == "siteid")
                {
                    column_name = "site_id";
                }
                if (column_name == "p/f")
                {
                    column_name = "pass/fail";
                }
                columns += "`" + column_name.Trim() + "`";
                if (i != content.LotResult.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert result表
            try
            {
                var valuesBuilder = new StringBuilder();
                var lotsResultParameters = new DynamicParameters();
                int lotsResultParameterIndex = 0;
                for (int i = 0; i < content.LotResult.Rows.Count; i++)
                {
                    //// 判斷 index=1 的Serial是否為空，若為空則跳過
                    if (!string.IsNullOrEmpty(content.LotResult.Rows[i]["Serial"].ToString()))
                    {
                        valuesBuilder.Append("(").Append(AddInsertParameter(lotsResultParameters, ref lotsResultParameterIndex, "lots_result", ConvertEmptyToDefaultString(lotId))).Append(",");
                        for (int j = 0; j < content.LotResult.Columns.Count; j++)
                        {
                            if ((content.LotResult.Columns[j].ColumnName == "SN Num" || content.LotResult.Columns[j].ColumnName == "SiteID" || content.LotResult.Columns[j].ColumnName == "real time" || content.LotResult.Columns[j].ColumnName == "X" || content.LotResult.Columns[j].ColumnName == "Y" || content.LotResult.Columns[j].ColumnName == "P/F") && content.LotResult.Rows[i][j].ToString().Trim() == string.Empty)
                            {
                                valuesBuilder.Append("NULL");
                            }
                            else if ((content.LotResult.Columns[j].ColumnName == "test time" || content.LotResult.Columns[j].ColumnName == "index time") && content.LotResult.Rows[i][j].ToString().Trim() == string.Empty)
                            {
                                valuesBuilder.Append("0");
                            }
                            else if (content.LotResult.Columns[j].ColumnName == "real time")
                            {
                                // 判斷是否為時間格式，若不符合則給NULL
                                DateTime out_dateTime;
                                if (DateTime.TryParse(content.LotResult.Rows[i][j].ToString().Trim(), out out_dateTime))
                                {
                                    valuesBuilder.Append(AddInsertParameter(lotsResultParameters, ref lotsResultParameterIndex, "lots_result", ConvertEmptyToDefaultString(content.LotResult.Rows[i][j].ToString())));
                                }
                                else
                                {
                                    valuesBuilder.Append("NULL");
                                }
                            }
                            else
                            {
                                valuesBuilder.Append(AddInsertParameter(lotsResultParameters, ref lotsResultParameterIndex, "lots_result", ConvertEmptyToDefaultString(content.LotResult.Rows[i][j].ToString())));
                            }
                            if (j != content.LotResult.Columns.Count - 1)
                            {
                                valuesBuilder.Append(",");
                            }
                        }
                        // 每cut_size個row就匯入一次
                        if (i != 0 && i % cut_size == 0 && valuesBuilder.Length > 0)
                        {
                            valuesBuilder.Append(")");
                            values = valuesBuilder.ToString();
                            values = values.Substring(1, values.Length - 2);
                            response2 = ExecuteInsert(DatabaseService, "lots_result", columns, values, lotsResultParameters);
                            if (!string.IsNullOrEmpty(response2.Error))
                            {
                                writeToLog.WriteErrorLog("'INSERT INTO lots_result' error:" + response2.Error);
                                return false;
                            }
                            valuesBuilder.Clear();
                            values = string.Empty;
                            lotsResultParameters = new DynamicParameters();
                            lotsResultParameterIndex = 0;
                        }
                        else if (i != content.LotResult.Rows.Count - 1)
                        {
                            valuesBuilder.Append("),");
                        }
                        else
                        {
                            valuesBuilder.Append(")");
                        }
                    }
                }
                values = valuesBuilder.ToString();
                if (!string.IsNullOrEmpty(values))
                {
                    values = values.Substring(1, values.Length - 2);
                    // 如果最後一個字元是')' 則移除
                    string last_str = values.Substring(values.Length - 1);
                    if (last_str == ")")
                    {
                        values = values.Substring(0, values.Length - 1);
                    }
                    response2 = ExecuteInsert(DatabaseService, "lots_result", columns, values, lotsResultParameters);
                    if (!string.IsNullOrEmpty(response2.Error))
                    {
                        writeToLog.WriteErrorLog("'INSERT INTO lots_result' response error:" + response2.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("'INSERT INTO lots_result' error:" + ex.Message);
                return false;
            }
            #endregion
            return true;
        }
        // MultiSpecRawData 匯入資料庫
        public bool ImportMultiSpecRawData(RawDataContentFormat content, DatabaseService DatabaseService, int siteId)
        {
            if (content.LotInfo.Rows.Count < 1 || content.LotStatistic.Tables.Count < 1) return false;
            // assign 需要 insert 的 欄位名稱 與 values
            string columns = string.Empty, values = string.Empty;
            string lotId = string.Empty;
            Execute_query_response response2;
            #region insert raw data 的 info 表格
            if (!MultiSiteRawDataLotInfoIsImported)
            {
                var lotsInfoParameters = new DynamicParameters();
                int lotsInfoParameterIndex = 0;
                for (int i = 0; i < content.LotInfo.Columns.Count; i++)
                {
                    string column_name = content.LotInfo.Columns[i].ColumnName.ToLower();
                    column_name = column_name.Split('(', ')')[0];
                    // 欄位名稱調整
                    if (column_name == "bondingdiagram") column_name = "bonding_diagram";
                    if (column_name == "pass without ocr".ToLower()) column_name = "pass_without_ocr";
                    if (column_name == "open without ocr".ToLower()) column_name = "open_without_ocr";
                    if (column_name == "short & others".ToLower()) column_name = "short_others";
                    if (column_name == "pass without ocr_ppm".ToLower()) column_name = "pass_without_ocr_ppm";
                    if (column_name == "open without ocr_ppm".ToLower()) column_name = "open_without_ocr_ppm";
                    if (column_name == "short & others_ppm".ToLower()) column_name = "short_others_ppm";
                    // 處理日期格式
                    if (column_name == "start" || column_name == "stop")
                    {
                        // 日期格式解析正確
                        content.LotInfo.Rows[0][i] = CustomizeDateTimeParser(content.LotInfo.Rows[0][i].ToString());
                    }
                    columns += "`" + column_name.Trim() + "`";
                    values += AddInsertParameter(lotsInfoParameters, ref lotsInfoParameterIndex, "lots_info", ConvertEmptyToDefaultString(content.LotInfo.Rows[0][i].ToString().Trim()));
                    if (i != content.LotInfo.Columns.Count - 1)
                    {
                        columns += ",";
                        values += ",";
                    }
                }
                try
                {
                    response2 = ExecuteInsert(DatabaseService, "lots_info", columns, values, lotsInfoParameters);
                    if (!string.IsNullOrEmpty(response2.Error))
                    {
                        writeToLog.WriteErrorLog("'INSERT INTO lots_info' error:" + response2.Error);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("'INSERT INTO lots_info' error:" + ex.Message);
                    writeToLog.WriteErrorLog("'INSERT INTO lots_info' error:" + ex.Message);
                    return false;
                }
                try
                {
                    // 取得當前 lot id 值
                    MultiSiteRawDataLotInfoIsImported = true;
                    MultiSiteRawDataLotInfoInsertId = response2.Data[0]["insertId"].ToString();
                }
                catch (Exception ex)
                {
                    writeToLog.WriteErrorLog("'取得當前 lot id 值 error:" + ex.Message);
                    Console.WriteLine(ex.ToString());
                    return false;
                }
            }
            #endregion
            // 取得當前 lot id 值
            lotId = MultiSiteRawDataLotInfoInsertId;
            //string lotId = "3";
            #region 驗證和轉換統計數據中的數字欄位
            try
            {
                //writeToLog.WriteInfoLog("開始驗證統計數據中的數字欄位");
                int validationCount = 0;
                int conversionCount = 0;
                // 定義需要數字驗證的統計欄位
                string[] statisticParametersToValidate = { "stdev", "avg", "cp", "cpk" };
                for (int i = 0; i < content.LotStatistic.Tables.Count; i++)
                {
                    if (content.LotStatistic.Tables[i].Rows.Count < 1) continue;
                    DataTable table = content.LotStatistic.Tables[i];
                    DataRow row = table.Rows[0]; // 統計表通常只有一行數據
                    // 取得項目名稱用於日誌記錄
                    string itemName = table.Columns.Contains("Item Name") ?
                                     (row["Item Name"]?.ToString() ?? string.Format("Table_{0}", i)) :
                                     string.Format("Table_{0}", i);
                    foreach (string paramName in statisticParametersToValidate)
                    {
                        if (table.Columns.Contains(paramName))
                        {
                            string originalValue = row[paramName]?.ToString() ?? "";
                            double validatedValue = ValidateAndConvertStatisticValue(originalValue, paramName, itemName);
                            // 如果值發生了變化，記錄轉換
                            if (originalValue != validatedValue.ToString())
                            {
                                conversionCount++;
                            }
                            row[paramName] = validatedValue;
                            validationCount++;
                        }
                    }
                }
                //writeToLog.WriteInfoLog(string.Format("統計數據驗證完成，共處理 {0} 個欄位，轉換 {1} 個非數字值", validationCount, conversionCount));
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog(string.Format("統計數據驗證過程發生錯誤：{0}", ex.Message));
                writeToLog.WriteErrorLog(string.Format("錯誤詳細信息：{0}", ex.StackTrace));
                return false;
            }
            #endregion
            #region insert raw data 的 statistic 表格
            columns = "`lot_id`,`site_id`,"; values = string.Empty;
            for (int i = 0; i < content.LotStatistic.Tables[0].Columns.Count; i++)
            {
                string column_name = content.LotStatistic.Tables[0].Columns[i].ColumnName.ToLower();
                column_name = column_name.Replace(" ", "_");
                //System.Text.RegularExpressions.Regex.Replace(column_name, " ", "_");
                if (column_name == "#_of_pass")
                {
                    column_name = "pass";
                }
                if (column_name == "#_of_fail")
                {
                    column_name = "fail";
                }
                columns += "`" + column_name.Trim() + "`";
                if (i != content.LotStatistic.Tables[0].Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert 統計值表
            int test_count = 0, cut_size = 0;
            int.TryParse(content.LotStatistic.Tables[0].Rows[0]["# of PASS"].ToString(), out test_count);
            int tableCount = content.LotStatistic.Tables.Count;
            cut_size = (test_count > 0 && test_count < 10000) ? 10000 / test_count : 1;
            int lotResultCount = content.LotResult.Rows.Count;
            Console.WriteLine("itemCount=" + tableCount + " lotResultCount= " + lotResultCount);
            try
            {
                var valuesBuilder = new StringBuilder();
                var lotsStatisticParameters = new DynamicParameters();
                int lotsStatisticParameterIndex = 0;
                for (int i = 0; i < content.LotStatistic.Tables.Count; i++)
                {
                    if (content.LotStatistic.Tables[i].Rows.Count < 1) continue;
                    valuesBuilder.Append("(").Append(AddInsertParameter(lotsStatisticParameters, ref lotsStatisticParameterIndex, "lots_statistic", ConvertEmptyToDefaultString(lotId))).Append(",");
                    valuesBuilder.Append(AddInsertParameter(lotsStatisticParameters, ref lotsStatisticParameterIndex, "lots_statistic", ConvertEmptyToDefaultString(siteId.ToString()))).Append(",");
                    valuesBuilder.Append(AddInsertParameters(lotsStatisticParameters, ref lotsStatisticParameterIndex, "lots_statistic", content.LotStatistic.Tables[i].Rows[0].ItemArray.Select(item => ConvertEmptyToDefaultString(item?.ToString()))));
                    // 每cut_size個row就匯入一次
                    if (i != 0 && i % cut_size == 0)
                    {
                        valuesBuilder.Append(")");
                        values = valuesBuilder.ToString();
                        values = values.Substring(1, values.Length - 2);
                        response2 = ExecuteInsert(DatabaseService, "lots_statistic", columns, values, lotsStatisticParameters);
                        if (!string.IsNullOrEmpty(response2.Error))
                        {
                            writeToLog.WriteErrorLog("'INSERT INTO lots_statistic' response error:" + response2.Error);
                            response2 = DeleteRawData(DatabaseService, lotId);
                            return false;
                        }
                        valuesBuilder.Clear();
                        values = string.Empty;
                        lotsStatisticParameters = new DynamicParameters();
                        lotsStatisticParameterIndex = 0;
                    }
                    else if (i != content.LotStatistic.Tables.Count - 1)
                    {
                        valuesBuilder.Append("),");
                    }
                    else
                    {
                        valuesBuilder.Append(")");
                    }
                }
                values = valuesBuilder.ToString();
                if (values.Length > 3)
                {
                    values = values.Substring(1, values.Length - 2);
                    response2 = ExecuteInsert(DatabaseService, "lots_statistic", columns, values, lotsStatisticParameters);
                    if (!string.IsNullOrEmpty(response2.Error))
                    {
                        writeToLog.WriteErrorLog("'INSERT INTO lots_statistic' response error:" + response2.Error);
                        response2 = DeleteRawData(DatabaseService, lotId);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("'INSERT INTO lots_statistic' error:" + ex.Message);
                response2 = DeleteRawData(DatabaseService, lotId);
                return false;
            }
            #endregion
            #region insert raw data 的 result 表格
            cut_size = (content.LotResult.Rows.Count > 5000) ? 5000 : content.LotResult.Rows.Count;
            columns = "`lot_id`,"; values = string.Empty;
            for (int i = 0; i < content.LotResult.Columns.Count; i++)
            {
                string column_name = content.LotResult.Columns[i].ColumnName.ToLower();
                column_name = column_name.Replace(" ", "_");
                if (column_name == "siteid")
                {
                    column_name = "site_id";
                }
                if (column_name == "p/f")
                {
                    column_name = "pass/fail";
                }
                columns += "`" + column_name.Trim() + "`";
                if (i != content.LotResult.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert result表
            try
            {
                var valuesBuilder = new StringBuilder();
                var lotsResultParameters = new DynamicParameters();
                int lotsResultParameterIndex = 0;
                for (int i = 0; i < content.LotResult.Rows.Count; i++)
                {
                    //// 判斷 index=1 的Serial是否為空，若為空則跳過
                    if (!string.IsNullOrEmpty(content.LotResult.Rows[i]["Serial"].ToString()))
                    {
                        valuesBuilder.Append("(").Append(AddInsertParameter(lotsResultParameters, ref lotsResultParameterIndex, "lots_result", ConvertEmptyToDefaultString(lotId))).Append(",");
                        for (int j = 0; j < content.LotResult.Columns.Count; j++)
                        {
                            if ((content.LotResult.Columns[j].ColumnName == "SN Num" || content.LotResult.Columns[j].ColumnName == "SiteID" || content.LotResult.Columns[j].ColumnName == "real time" || content.LotResult.Columns[j].ColumnName == "X" || content.LotResult.Columns[j].ColumnName == "Y" || content.LotResult.Columns[j].ColumnName == "P/F") && content.LotResult.Rows[i][j].ToString().Trim() == string.Empty)
                            {
                                valuesBuilder.Append("NULL");
                            }
                            else if ((content.LotResult.Columns[j].ColumnName == "test time" || content.LotResult.Columns[j].ColumnName == "index time") && content.LotResult.Rows[i][j].ToString().Trim() == string.Empty)
                            {
                                valuesBuilder.Append("0");
                            }
                            else if (content.LotResult.Columns[j].ColumnName == "real time")
                            {
                                // 判斷是否為時間格式，若不符合則給NULL
                                DateTime out_dateTime;
                                if (DateTime.TryParse(content.LotResult.Rows[i][j].ToString().Trim(), out out_dateTime))
                                {
                                    valuesBuilder.Append(AddInsertParameter(lotsResultParameters, ref lotsResultParameterIndex, "lots_result", ConvertEmptyToDefaultString(content.LotResult.Rows[i][j].ToString())));
                                }
                                else
                                {
                                    valuesBuilder.Append("NULL");
                                }
                            }
                            else
                            {
                                valuesBuilder.Append(AddInsertParameter(lotsResultParameters, ref lotsResultParameterIndex, "lots_result", ConvertEmptyToDefaultString(content.LotResult.Rows[i][j].ToString())));
                            }
                            if (j != content.LotResult.Columns.Count - 1)
                            {
                                valuesBuilder.Append(",");
                            }
                        }
                        // 每cut_size個row就匯入一次
                        if (i != 0 && i % cut_size == 0 && valuesBuilder.Length > 0)
                        {
                            valuesBuilder.Append(")");
                            values = valuesBuilder.ToString();
                            values = values.Substring(1, values.Length - 2);
                            response2 = ExecuteInsert(DatabaseService, "lots_result", columns, values, lotsResultParameters);
                            if (!string.IsNullOrEmpty(response2.Error))
                            {
                                writeToLog.WriteErrorLog("'INSERT INTO lots_result' error:" + response2.Error);
                                return false;
                            }
                            valuesBuilder.Clear();
                            values = string.Empty;
                            lotsResultParameters = new DynamicParameters();
                            lotsResultParameterIndex = 0;
                        }
                        else if (i != content.LotResult.Rows.Count - 1)
                        {
                            valuesBuilder.Append("),");
                        }
                        else
                        {
                            valuesBuilder.Append(")");
                        }
                    }
                }
                values = valuesBuilder.ToString();
                if (!string.IsNullOrEmpty(values))
                {
                    values = values.Substring(1, values.Length - 2);
                    // 如果最後一個字元是')' 則移除
                    string last_str = values.Substring(values.Length - 1);
                    if (last_str == ")")
                    {
                        values = values.Substring(0, values.Length - 1);
                    }
                    response2 = ExecuteInsert(DatabaseService, "lots_result", columns, values, lotsResultParameters);
                    if (!string.IsNullOrEmpty(response2.Error))
                    {
                        writeToLog.WriteErrorLog("'INSERT INTO lots_result' response error:" + response2.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("'INSERT INTO lots_result' error:" + ex.Message);
                return false;
            }
            #endregion
            return true;
        }
        // Tester Status 匯入資料庫
        public bool ImportTesterStatus(TestStatusContentFormat content, DatabaseService DatabaseService)
        {
            if (content.Tester_device_info.Rows.Count < 1 || content.Tester_status.Rows.Count < 1 || content.Tester_sw_version.Rows.Count < 1) return false;
            // assign 需要 insert 的 欄位名稱 與 values
            string columns = string.Empty, values = string.Empty;
            Execute_query_response response;
            #region insert `tester_device_info`
            var testerDeviceInfoParameters = new DynamicParameters();
            int testerDeviceInfoParameterIndex = 0;
            for (int i = 0; i < content.Tester_device_info.Columns.Count; i++)
            {
                string column_name = content.Tester_device_info.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                // 欄位名稱調整
                if (column_name == "prober_/_handler") column_name = "prober/handler";
                if (column_name == "l/b_id") column_name = "L/B_id";
                if (column_name == "handler_repair_starttime") column_name = "handler_repair_start_time";
                if (column_name == "handler_repair_endtime") column_name = "handler_repair_end_time";
                string[] numTypeColumn = { "efficiency_check", "ui_flow_checksum", "yield", "lead_count", "site_qty", "bd_leak", "pg_leak", "wireclose_leak" };
                if (numTypeColumn.Contains(column_name) && (content.Tester_device_info.Rows[0][i].ToString().Trim() == string.Empty || content.Tester_device_info.Rows[0][i].ToString().Trim() == "NA"))
                {
                    values += "NULL";
                }
                else
                {
                    // 路徑的(\) 要處理成 (\\)
                    if (column_name == "program_path")
                    {
                        values += AddInsertParameter(testerDeviceInfoParameters, ref testerDeviceInfoParameterIndex, "tester_device_info", ConvertEmptyToDefaultString(content.Tester_device_info.Rows[0][i].ToString()).Replace(@"\", @"\\"));
                    }
                    else if (column_name == "start_time" || column_name == "end_time")
                    {
                        DateTime datetime = new DateTime();
                        if (DateTime.TryParse(content.Tester_device_info.Rows[0][i].ToString().Trim(), out datetime))
                        {
                            values += AddInsertParameter(testerDeviceInfoParameters, ref testerDeviceInfoParameterIndex, "tester_device_info", datetime.ToString("yyyy-MM-dd HH:mm:ss"));
                        }
                        else
                        {
                            values += "null";
                        }
                    }
                    else
                    {
                        values += AddInsertParameter(testerDeviceInfoParameters, ref testerDeviceInfoParameterIndex, "tester_device_info", ConvertEmptyToDefaultString(content.Tester_device_info.Rows[0][i].ToString()));
                    }
                }
                columns += "`" + column_name.Trim() + "`";
                if (i != content.Tester_device_info.Columns.Count - 1)
                {
                    columns += ",";
                    values += ",";
                }
            }
            try
            {
                response = ExecuteInsert(DatabaseService, "tester_device_info", columns, values, testerDeviceInfoParameters);
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteErrorLog("'INSERT INTO tester_device_info' error:" + response.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                writeToLog.WriteErrorLog("'INSERT INTO tester_device_info' error:" + ex.Message);
                return false;
            }
            #endregion
            string device_info_Id = string.Empty;
            try
            {
                device_info_Id = response.Data[0]["insertId"].ToString();
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("get tester_device_info insertId error:" + ex.Message);
                return false;
            }
            #region insert `tester_status`
            columns = "`device_info_Id`,"; values = string.Empty;
            for (int i = 0; i < content.Tester_status.Columns.Count; i++)
            {
                string column_name = content.Tester_status.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                if (column_name == "diff_time_(die)") column_name = "diff_time_die";
                if (column_name == "end_time_(die)") column_name = "end_time_die";
                if (column_name == "first_time_(die)") column_name = "first_time_die";
                if (column_name == "diff_time_(file)") column_name = "diff_time_file";
                if (column_name == "pass_/_fail") column_name = "pass/fail";
                columns += "`" + column_name + "`";
                if (i != content.Tester_status.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert
            try
            {
                for (int i = 0; i < content.Tester_status.Rows.Count; i++)
                {
                    // 只存一行資料，超過就不存
                    if (i > 0) break;
                    var testerStatusParameters = new DynamicParameters();
                    int testerStatusParameterIndex = 0;
                    values = AddInsertParameter(testerStatusParameters, ref testerStatusParameterIndex, "tester_status", ConvertEmptyToDefaultString(device_info_Id)) + ",";
                    for (int j = 0; j < content.Tester_status.Columns.Count; j++)
                    {
                        string columnName = content.Tester_status.Columns[j].ColumnName;
                        string[] doubleTypeColumn = { "Duts", "UPH", "Avg test time", "Max test time", "Min test time", "Avg index test time", "Max index test time", "Min index test time", "Diff time (die)", "End time (die)", "First time (die)", "Diff time (file)" };
                        if (doubleTypeColumn.Contains(columnName) && (content.Tester_status.Rows[0][j].ToString().Trim() == string.Empty || content.Tester_status.Rows[0][j].ToString().Trim() == "NA"))
                        {
                            values += "NULL";
                        }
                        else
                        {
                            values += AddInsertParameter(testerStatusParameters, ref testerStatusParameterIndex, "tester_status", ConvertEmptyToDefaultString(content.Tester_status.Rows[i][j].ToString()));
                        }
                        if (j != content.Tester_status.Columns.Count - 1)
                        {
                            values += ",";
                        }
                    }
                    response = ExecuteInsert(DatabaseService, "tester_status", columns, values, testerStatusParameters);
                    if (!string.IsNullOrEmpty(response.Error))
                    {
                        writeToLog.WriteErrorLog("'INSERT INTO tester_status' error:" + response.Error);
                        response = DeleteTesterStatus(DatabaseService, device_info_Id);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                writeToLog.WriteErrorLog("'INSERT INTO tester_status' error:" + ex.Message);
                response = DeleteTesterStatus(DatabaseService, device_info_Id);
                return false;
            }
            #endregion
            #region insert `tester_sw_version`
            columns = "`device_info_Id`,"; values = string.Empty;
            for (int i = 0; i < content.Tester_sw_version.Columns.Count; i++)
            {
                string column_name = content.Tester_sw_version.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                column_name = column_name.Trim().Replace(".", "_");
                if (column_name == "dct_i-v_curve_tool_md5") column_name = "dct_iv_curve_tool_md5";
                if (column_name == "simplificationui_md5") column_name = "simplification_ui_md5";
                if (column_name == "autolearn_pui_version") column_name = "auto_learn_pui_version";
                columns += "`" + column_name + "`";
                if (i != content.Tester_sw_version.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert
            try
            {
                for (int i = 0; i < content.Tester_sw_version.Rows.Count; i++)
                {
                    var testerSwVersionParameters = new DynamicParameters();
                    int testerSwVersionParameterIndex = 0;
                    values = AddInsertParameter(testerSwVersionParameters, ref testerSwVersionParameterIndex, "tester_sw_version", ConvertEmptyToDefaultString(device_info_Id)) + ",";
                    for (int j = 0; j < content.Tester_sw_version.Columns.Count; j++)
                    {
                        string columnName = content.Tester_sw_version.Columns[j].ColumnName;
                        values += AddInsertParameter(testerSwVersionParameters, ref testerSwVersionParameterIndex, "tester_sw_version", ConvertEmptyToDefaultString(content.Tester_sw_version.Rows[i][j].ToString()));
                        if (j != content.Tester_sw_version.Columns.Count - 1)
                        {
                            values += ",";
                        }
                    }
                    response = ExecuteInsert(DatabaseService, "tester_sw_version", columns, values, testerSwVersionParameters);
                    if (!string.IsNullOrEmpty(response.Error))
                    {
                        writeToLog.WriteErrorLog("'INSERT INTO tester_sw_version' error:" + response.Error);
                        response = DeleteTesterStatus(DatabaseService, device_info_Id);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("'INSERT INTO tester_sw_version' error:" + ex.Message);
                response = DeleteTesterStatus(DatabaseService, device_info_Id);
                return false;
            }
            #endregion
            #region insert `tester_production_analysis`
            columns = "`device_info_Id`,"; values = string.Empty;
            for (int i = 0; i < content.Tester_production_analysis.Columns.Count; i++)
            {
                string column_name = content.Tester_production_analysis.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                columns += "`" + column_name + "`";
                if (i != content.Tester_production_analysis.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert
            try
            {
                for (int i = 0; i < content.Tester_production_analysis.Rows.Count; i++)
                {
                    var testerProductionAnalysisParameters = new DynamicParameters();
                    int testerProductionAnalysisParameterIndex = 0;
                    values = AddInsertParameter(testerProductionAnalysisParameters, ref testerProductionAnalysisParameterIndex, "tester_production_analysis", ConvertEmptyToDefaultString(device_info_Id)) + ",";
                    for (int j = 0; j < content.Tester_production_analysis.Columns.Count; j++)
                    {
                        string columnName = content.Tester_production_analysis.Columns[j].ColumnName;
                        if (content.Tester_production_analysis.Rows[0][j].ToString().Trim() == string.Empty || content.Tester_production_analysis.Rows[0][j].ToString().Trim() == "NA")
                        {
                            values += "NULL";
                        }
                        else
                        {
                            values += AddInsertParameter(testerProductionAnalysisParameters, ref testerProductionAnalysisParameterIndex, "tester_production_analysis", ConvertEmptyToDefaultString(content.Tester_production_analysis.Rows[i][j].ToString()));
                        }
                        if (j != content.Tester_production_analysis.Columns.Count - 1)
                        {
                            values += ",";
                        }
                    }
                    response = ExecuteInsert(DatabaseService, "tester_production_analysis", columns, values, testerProductionAnalysisParameters);
                    if (!string.IsNullOrEmpty(response.Error))
                    {
                        writeToLog.WriteErrorLog("'INSERT INTO tester_production_analysis' error:" + response.Error);
                        response = DeleteTesterStatus(DatabaseService, device_info_Id);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog("'INSERT INTO tester_production_analysis'  error:" + ex.Message);
                response = DeleteTesterStatus(DatabaseService, device_info_Id);
                return false;
            }
            #endregion
            return true;
        }
        // UI Status 匯入資料庫
        public bool ImportUIStatus(UIStatusContentFormat content, DatabaseService DatabaseService)
        {
            if (content.UI_status.Rows.Count < 1) return false;
            // assign 需要 insert 的 欄位名稱 與 values
            string columns = string.Empty, values = string.Empty;
            Execute_query_response response;
            string mac_address, area, factory, os_machine, date;
            for (int j = 0; j < content.UI_status.Columns.Count; j++)
            {
                string column_name = content.UI_status.Columns[j].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                columns += "`" + column_name.Trim() + "`";
                if (j != content.UI_status.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            #region insert `ui_status`
            for (int i = 0; i < content.UI_status.Rows.Count; i++)
            {
                mac_address = content.UI_status.Rows[i]["Mac_Address"].ToString().Trim();
                area = content.UI_status.Rows[i]["Area"].ToString().Trim();
                factory = content.UI_status.Rows[i]["Factory"].ToString().Trim();
                os_machine = content.UI_status.Rows[i]["OS_Machine"].ToString().Trim();
                date = content.UI_status.Rows[i]["Date"].ToString().Trim();
                values = string.Empty;
                var uiStatusParameters = new DynamicParameters();
                int uiStatusParameterIndex = 0;
                for (int j = 0; j < content.UI_status.Columns.Count; j++)
                {
                    string[] numTypeColumn = { "auto_learn", "dct_product_file_setting_ui", "dct_login_ui", "os_self_diag_2k", "pattonkan_ui", "dct_i_v_curve_tool", "os_tester_100ma_vi", "os_tester_2a_vi", "os_tester_lcr_meter", "wire_assignment_tool", "bga_highlight_tool", "simplificationui", "os_scan_tool", "dct_uploadtp_ui", "dct_autodownloadtp", "dct_sw_control_tool", "dct_downloadtp_kh" };
                    if (numTypeColumn.Contains(content.UI_status.Columns[j].ColumnName.ToLower()) && (content.UI_status.Rows[i][j].ToString().Trim() == string.Empty || content.UI_status.Rows[i][j].ToString().Trim() == "NA"))
                    {
                        values += "NULL";
                    }
                    else if (content.UI_status.Columns[j].ColumnName.ToLower() == "date" && (content.UI_status.Rows[i][j].ToString().Trim() == string.Empty || content.UI_status.Rows[i][j].ToString().Trim() == "0"))
                    {
                        date = "null";
                        values += "NULL";
                    }
                    else
                    {
                        values += AddInsertParameter(uiStatusParameters, ref uiStatusParameterIndex, "ui_status", ConvertEmptyToDefaultString(content.UI_status.Rows[i][j].ToString()));
                    }
                    if (j != content.UI_status.Columns.Count - 1)
                    {
                        values += ",";
                    }
                }
                try
                {
                    response = ExecuteInsert(DatabaseService, "ui_status", columns, values, uiStatusParameters);
                    if (!string.IsNullOrEmpty(response.Error))
                    {
                        writeToLog.WriteErrorLog("'INSERT INTO ui_status' error:" + response.Error);
                        return false;
                    }
                    //}
                }
                catch (Exception ex)
                {
                    writeToLog.WriteErrorLog("'INSERT INTO ui_status' error:" + ex.Message);
                    return false;
                }
            }
            #endregion
            return true;
        }
        // Fail Pin Log 匯入資料庫
        public bool ImportFailPinLog(FailPinLogContentFormat content, DatabaseService DatabaseService)
        {
            if (content.Fail_pin_rate_info.Rows.Count < 1) return false;
            // assign 需要 insert 的 欄位名稱 與 values
            string columns = string.Empty, values = string.Empty;
            Execute_query_response response;
            #region insert `fail_pin_rate_info`
            var failPinRateInfoParameters = new DynamicParameters();
            int failPinRateInfoParameterIndex = 0;
            for (int i = 0; i < content.Fail_pin_rate_info.Columns.Count; i++)
            {
                string column_name = content.Fail_pin_rate_info.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                // 欄位名稱調整
                //if (column_name == "db_key") column_name = "db_version";
                if (!string.IsNullOrEmpty(content.Fail_pin_rate_info.Rows[0][i].ToString().Trim()))
                {
                    columns += "`" + column_name.Trim() + "`";
                    values += AddInsertParameter(failPinRateInfoParameters, ref failPinRateInfoParameterIndex, "fail_pin_rate_info", ConvertEmptyToDefaultString(content.Fail_pin_rate_info.Rows[0][i].ToString()));
                }
                else
                {
                    columns = columns.Substring(0, columns.Length - 1);
                    values = values.Substring(0, values.Length - 1);
                }
                if (i != content.Fail_pin_rate_info.Columns.Count - 1)
                {
                    columns += ",";
                    values += ",";
                }
            }
            try
            {
                response = ExecuteInsert(DatabaseService, "fail_pin_rate_info", columns, values, failPinRateInfoParameters);
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteErrorLog("'INSERT INTO fail_pin_rate_info' error:" + response.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                writeToLog.WriteErrorLog("'INSERT INTO fail_pin_rate_info' error:" + ex.Message);
                return false;
            }
            #endregion
            string fail_pin_rate_info_Id = response.Data[0]["insertId"].ToString();
            List<string> fail_pin_rate_list_Id = new List<string>();
            #region insert `fail_pin_rate_list`
            columns = "`fail_pin_rate_info_Id`,"; values = string.Empty;
            for (int i = 0; i < content.Fail_pin_rate_list.Columns.Count; i++)
            {
                string column_name = content.Fail_pin_rate_list.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                columns += "`" + column_name + "`";
                if (i != content.Fail_pin_rate_list.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert
            try
            {
                for (int i = 0; i < content.Fail_pin_rate_list.Rows.Count; i++)
                {
                    var failPinRateListParameters = new DynamicParameters();
                    int failPinRateListParameterIndex = 0;
                    values = AddInsertParameter(failPinRateListParameters, ref failPinRateListParameterIndex, "fail_pin_rate_list", ConvertEmptyToDefaultString(fail_pin_rate_info_Id)) + ",";
                    for (int j = 0; j < content.Fail_pin_rate_list.Columns.Count; j++)
                    {
                        string columnName = content.Fail_pin_rate_list.Columns[j].ColumnName;
                        string[] doubleTypeColumn = { "dut", "site" };
                        if (doubleTypeColumn.Contains(columnName) && (content.Fail_pin_rate_list.Rows[0][j].ToString().Trim() == string.Empty || content.Fail_pin_rate_list.Rows[0][j].ToString().Trim() == "NA"))
                        {
                            values += "NULL";
                        }
                        else
                        {
                            values += AddInsertParameter(failPinRateListParameters, ref failPinRateListParameterIndex, "fail_pin_rate_list", ConvertEmptyToDefaultString(content.Fail_pin_rate_list.Rows[i][j].ToString()));
                        }
                        if (j != content.Fail_pin_rate_list.Columns.Count - 1)
                        {
                            values += ",";
                        }
                    }
                    response = ExecuteInsert(DatabaseService, "fail_pin_rate_list", columns, values, failPinRateListParameters);
                    if (!string.IsNullOrEmpty(response.Error))
                    {
                        writeToLog.WriteErrorLog("'INSERT INTO fail_pin_rate_list' error:" + response.Error);
                        response = DeleteFailPinLog(DatabaseService, fail_pin_rate_info_Id);
                        return false;
                    }
                    // 將此筆insert的fail_pin_rate_list_id保存至陣列
                    fail_pin_rate_list_Id.Add(response.Data[0]["insertId"].ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                writeToLog.WriteErrorLog("'INSERT INTO fail_pin_rate_list' error:" + ex.Message);
                response = DeleteFailPinLog(DatabaseService, fail_pin_rate_info_Id);
                return false;
            }
            #endregion
            #region insert `fail_pin_rate_list_pin_ball`
            columns = string.Empty; values = string.Empty;
            for (int i = 0; i < content.Fail_pin_rate_list_pin_ball.Columns.Count; i++)
            {
                string column_name = content.Fail_pin_rate_list_pin_ball.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                columns += "`" + column_name + "`";
                if (i != content.Fail_pin_rate_list_pin_ball.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert
            try
            {
                var valuesBuilder = new StringBuilder();
                var failPinRatePinBallParameters = new DynamicParameters();
                int failPinRatePinBallParameterIndex = 0;
                for (int i = 0; i < content.Fail_pin_rate_list_pin_ball.Rows.Count; i++)
                {
                    valuesBuilder.Append("(");
                    for (int j = 0; j < content.Fail_pin_rate_list_pin_ball.Columns.Count; j++)
                    {
                        string columnName = content.Fail_pin_rate_list_pin_ball.Columns[j].ColumnName;
                        if (columnName != "fail_pin_rate_list_id")
                        {
                            valuesBuilder.Append(AddInsertParameter(failPinRatePinBallParameters, ref failPinRatePinBallParameterIndex, "fail_pin_rate_list_pin_ball", ConvertEmptyToDefaultString(content.Fail_pin_rate_list_pin_ball.Rows[i][j].ToString())));
                        }
                        else
                        {
                            string val = ConvertEmptyToDefaultString(content.Fail_pin_rate_list_pin_ball.Rows[i][j].ToString());
                            valuesBuilder.Append(AddInsertParameter(failPinRatePinBallParameters, ref failPinRatePinBallParameterIndex, "fail_pin_rate_list_pin_ball", fail_pin_rate_list_Id[int.Parse(val) - 1].ToString()));
                        }
                        if (j != content.Fail_pin_rate_list_pin_ball.Columns.Count - 1)
                        {
                            valuesBuilder.Append(",");
                        }
                    }
                    // 每50個row就匯入一次
                    if (i != 0 && i % 50 == 0)
                    {
                        valuesBuilder.Append(")");
                        values = valuesBuilder.ToString();
                        values = values.Substring(1, values.Length - 2);
                        response = ExecuteInsert(DatabaseService, "fail_pin_rate_list_pin_ball", columns, values, failPinRatePinBallParameters);
                        if (!string.IsNullOrEmpty(response.Error))
                        {
                            writeToLog.WriteErrorLog("'INSERT INTO fail_pin_rate_list_pin_ball' error:" + response.Error);
                            response = DeleteFailPinLog(DatabaseService, fail_pin_rate_info_Id);
                            return false;
                        }
                        valuesBuilder.Clear();
                        values = string.Empty;
                        failPinRatePinBallParameters = new DynamicParameters();
                        failPinRatePinBallParameterIndex = 0;
                    }
                    else if (i != content.Fail_pin_rate_list_pin_ball.Rows.Count - 1)
                    {
                        valuesBuilder.Append("),");
                    }
                    else
                    {
                        valuesBuilder.Append(")");
                    }
                }
                values = valuesBuilder.ToString();
                if (!string.IsNullOrEmpty(values))
                {
                    values = values.Substring(1, values.Length - 2);
                    response = ExecuteInsert(DatabaseService, "fail_pin_rate_list_pin_ball", columns, values, failPinRatePinBallParameters);
                    if (!string.IsNullOrEmpty(response.Error))
                    {
                        writeToLog.WriteErrorLog("'INSERT INTO fail_pin_rate_list_pin_ball' error:" + response.Error);
                        response = DeleteFailPinLog(DatabaseService, fail_pin_rate_info_Id);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                writeToLog.WriteErrorLog("'INSERT INTO fail_pin_rate_list_pin_ball' error:" + ex.Message);
                response = DeleteFailPinLog(DatabaseService, fail_pin_rate_info_Id);
                return false;
            }
            #endregion
            #region insert `fail_pin_rate_test_result`             2024/3/1 新增
            columns = "`fail_pin_rate_list_id`,`item_name`,`open`,`short`,`vmeas`";
            // 開始逐一insert
            try
            {
                var valuesBuilder = new StringBuilder();
                var failPinRateTestResultParameters = new DynamicParameters();
                int failPinRateTestResultParameterIndex = 0;
                for (int table_i = 0; table_i < content.Fail_pin_rate_list_test_result.Tables.Count; table_i++)
                {
                    for (int i = 0; i < content.Fail_pin_rate_list_test_result.Tables[table_i].Rows.Count; i++)
                    {
                        valuesBuilder.Append("(").Append(AddInsertParameter(failPinRateTestResultParameters, ref failPinRateTestResultParameterIndex, "fail_pin_rate_test_result", ConvertEmptyToDefaultString(fail_pin_rate_list_Id[table_i].ToString()))).Append(",");
                        for (int j = 0; j < content.Fail_pin_rate_list_test_result.Tables[table_i].Columns.Count; j++)
                        {
                            if (j > 0 && string.IsNullOrEmpty(content.Fail_pin_rate_list_test_result.Tables[table_i].Rows[i][j].ToString()))
                            {
                                valuesBuilder.Append("NULL");
                            }
                            else
                            {
                                valuesBuilder.Append(AddInsertParameter(failPinRateTestResultParameters, ref failPinRateTestResultParameterIndex, "fail_pin_rate_test_result", ConvertEmptyToDefaultString(content.Fail_pin_rate_list_test_result.Tables[table_i].Rows[i][j].ToString())));
                            }
                            if (j != content.Fail_pin_rate_list_test_result.Tables[table_i].Columns.Count - 1)
                            {
                                valuesBuilder.Append(",");
                            }
                        }
                        // 每50個row就匯入一次
                        if (i != 0 && i % 50 == 0)
                        {
                            valuesBuilder.Append(")");
                            values = valuesBuilder.ToString();
                            values = values.Substring(1, values.Length - 2);
                            response = ExecuteInsert(DatabaseService, "fail_pin_rate_test_result", columns, values, failPinRateTestResultParameters);
                            if (!string.IsNullOrEmpty(response.Error))
                            {
                                writeToLog.WriteErrorLog("'INSERT INTO fail_pin_rate_test_result' error:" + response.Error);
                                response = DeleteFailPinLog(DatabaseService, fail_pin_rate_info_Id);
                                return false;
                            }
                            valuesBuilder.Clear();
                            values = string.Empty;
                            failPinRateTestResultParameters = new DynamicParameters();
                            failPinRateTestResultParameterIndex = 0;
                        }
                        else if (table_i != content.Fail_pin_rate_list_test_result.Tables.Count - 1)
                        {
                            valuesBuilder.Append("),");
                        }
                        else
                        {
                            valuesBuilder.Append("),");
                        }
                    }
                }
                values = valuesBuilder.ToString();
                if (!string.IsNullOrEmpty(values))
                {
                    if (values[values.Length - 1] == ',')
                    {
                        values = values.Substring(1, values.Length - 3);
                    }
                    else
                    {
                        values = values.Substring(1, values.Length - 2);
                    }
                    response = ExecuteInsert(DatabaseService, "fail_pin_rate_test_result", columns, values, failPinRateTestResultParameters);
                    if (!string.IsNullOrEmpty(response.Error))
                    {
                        writeToLog.WriteErrorLog("'INSERT INTO fail_pin_rate_test_result' error:" + response.Error);
                        response = DeleteFailPinLog(DatabaseService, fail_pin_rate_info_Id);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                writeToLog.WriteErrorLog("'INSERT INTO fail_pin_rate_test_result' error:" + ex.Message);
                response = DeleteFailPinLog(DatabaseService, fail_pin_rate_info_Id);
                return false;
            }
            #endregion
            return true;
        }
        public Execute_query_response ExecuteInsert(DatabaseService DatabaseService, string tableName, string columns, string values, object parameters = null)
        {
            try
            {
                return ExecuteInsert(DatabaseService, tableName, BuildInsertQuery(tableName, columns, values, parameters));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                writeToLog.WriteErrorLog($"INSERT {tableName} error : {ex.Message}");
                return new Execute_query_response { Error = ex.Message };
            }
        }

        internal Execute_query_response ExecuteInsert(DatabaseService DatabaseService, string tableName, Execute_query execute_query)
        {
            try
            {
                ValidateSqlIdentifier(tableName, nameof(tableName));
                // 檢查資料庫和資料表是否存在
                if (!DatabaseService.CheckDatabaseAndTableExists(tableName))
                {
                    writeToLog.WriteErrorLog($"資料庫或資料表 {tableName} 不存在");
                    return new Execute_query_response { Error = $"Database or table {tableName} does not exist" };
                }
                Execute_query_response response = DatabaseService.ExecuteSql(execute_query, "insert");
                if (!string.IsNullOrEmpty(response.Error))
                {
                    writeToLog.WriteErrorLog($"SQL Operation: INSERT {tableName}");
                    writeToLog.WriteErrorLog($"Error: {response.Error}");
                    writeToLog.WriteErrorLog($"INSERT {tableName} error");
                }
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                writeToLog.WriteErrorLog($"INSERT {tableName} error : {ex.Message}");
                return new Execute_query_response { Error = ex.Message };
            }
        }

        internal static Execute_query BuildDbKeyExistsQuery(string tableName, string dbKey)
        {
            ValidateSqlIdentifier(tableName, nameof(tableName));

            return new Execute_query
            {
                Query = "SELECT db_key FROM " + tableName + " where db_key=@dbKey;",
                Parameters = new { dbKey }
            };
        }

        internal static Execute_query BuildInsertQuery(string tableName, string columns, string values, object parameters = null)
        {
            ValidateSqlIdentifier(tableName, nameof(tableName));
            ValidateColumnList(columns, nameof(columns));

            return new Execute_query
            {
                Query = "INSERT INTO " + tableName + "(" + columns + ") VALUES (" + values + ");",
                Parameters = parameters
            };
        }

        internal static string AddInsertParameter(DynamicParameters parameters, ref int parameterIndex, string prefix, string value)
        {
            string parameterName = prefix + "_" + parameterIndex.ToString(CultureInfo.InvariantCulture);
            parameterIndex++;
            parameters.Add(parameterName, value);
            return "@" + parameterName;
        }

        internal static string AddInsertParameters(DynamicParameters parameters, ref int parameterIndex, string prefix, IEnumerable<string> values)
        {
            var placeholders = new List<string>();
            foreach (string value in values)
            {
                placeholders.Add(AddInsertParameter(parameters, ref parameterIndex, prefix, value));
            }
            return string.Join(",", placeholders);
        }

        private static void ValidateSqlIdentifier(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
            {
                throw new ArgumentException("Unsafe SQL identifier", paramName);
            }
        }

        private static void ValidateColumnList(string columns, string paramName)
        {
            if (string.IsNullOrWhiteSpace(columns))
            {
                throw new ArgumentException("Unsafe SQL columns", paramName);
            }

            foreach (string part in columns.Split(','))
            {
                string column = part.Trim();
                if (column.Length < 3 || column[0] != '`' || column[column.Length - 1] != '`' ||
                    column.IndexOf('`', 1, column.Length - 2) >= 0 ||
                    column.Contains(";") || column.Contains("--") || column.Contains("/*"))
                {
                    throw new ArgumentException("Unsafe SQL columns", paramName);
                }
            }
        }
        private Execute_query_response DeleteRawData(DatabaseService DatabaseService, string lot_id)
        {
            // 檢查資料庫和相關資料表是否存在
            string[] requiredTables = { "lots_info", "lots_statistic", "lots_result" };
            foreach (string tableName in requiredTables)
            {
                if (!DatabaseService.CheckDatabaseAndTableExists(tableName))
                {
                    writeToLog.WriteErrorLog($"資料庫或資料表 {tableName} 不存在，無法執行刪除操作");
                    return new Execute_query_response { Error = $"Database or table {tableName} does not exist" };
                }
            }
            Execute_query execute_query = BuildDeleteRawDataQuery(lot_id);
            Execute_query_response response = DatabaseService.ExecuteSql(execute_query, "delete");
            if (!string.IsNullOrEmpty(response.Error))
            {
                writeToLog.WriteErrorLog($"SQL Query: {execute_query.Query}");
                writeToLog.WriteErrorLog($"Error: {response.Error}");
                writeToLog.WriteErrorLog("DELETE lots_info, lots_statistic, lots_result error");
            }
            return response;
        }

        internal static Execute_query BuildDeleteRawDataQuery(string lotId)
        {
            return new Execute_query
            {
                Query = @"DELETE t1, t2, t3
										  FROM lots_info t1
										  LEFT JOIN lots_statistic t2 ON t1.id = t2.lot_id
										  LEFT JOIN lots_result t3 ON t1.id = t3.lot_id
									WHERE t1.id = @lotId; ",
                Parameters = new { lotId }
            };
        }

        private Execute_query_response DeleteTesterStatus(DatabaseService DatabaseService, string device_info_id)
        {
            // 檢查資料庫和相關資料表是否存在
            string[] requiredTables = { "tester_device_info", "tester_status", "tester_sw_version", "tester_production_analysis" };
            foreach (string tableName in requiredTables)
            {
                if (!DatabaseService.CheckDatabaseAndTableExists(tableName))
                {
                    writeToLog.WriteErrorLog($"資料庫或資料表 {tableName} 不存在，無法執行刪除操作");
                    return new Execute_query_response { Error = $"Database or table {tableName} does not exist" };
                }
            }
            Execute_query execute_query = BuildDeleteTesterStatusQuery(device_info_id);
            Execute_query_response response = DatabaseService.ExecuteSql(execute_query, "delete");
            if (!string.IsNullOrEmpty(response.Error))
            {
                writeToLog.WriteErrorLog($"SQL Query: {execute_query.Query}");
                writeToLog.WriteErrorLog($"Error: {response.Error}");
                writeToLog.WriteErrorLog("DELETE tester_device_info, tester_status, tester_sw_version, tester_production_analysis error");
            }
            return response;
        }

        internal static Execute_query BuildDeleteTesterStatusQuery(string deviceInfoId)
        {
            return new Execute_query
            {
                Query = @"DELETE t1, t2, t3, t4
										  FROM tester_device_info t1
										  LEFT JOIN tester_status t2 ON t1.id = t2.device_info_id
										  LEFT JOIN tester_sw_version t3 ON t1.id = t3.device_info_id
										  LEFT JOIN tester_production_analysis t4 ON t1.id = t4.device_info_id
									 WHERE t1.id = @deviceInfoId; ",
                Parameters = new { deviceInfoId }
            };
        }

        private Execute_query_response DeleteFailPinLog(DatabaseService DatabaseService, string fail_pin_id)
        {
            // 檢查資料庫和相關資料表是否存在
            string[] requiredTables = { "fail_pin_rate_info", "fail_pin_rate_list", "fail_pin_rate_list_pin_ball", "fail_pin_rate_test_result" };
            foreach (string tableName in requiredTables)
            {
                if (!DatabaseService.CheckDatabaseAndTableExists(tableName))
                {
                    writeToLog.WriteErrorLog($"資料庫或資料表 {tableName} 不存在，無法執行刪除操作");
                    return new Execute_query_response { Error = $"Database or table {tableName} does not exist" };
                }
            }
            Execute_query[] deleteQueries = BuildDeleteFailPinLogQueries(fail_pin_id);
            Execute_query execute_query = deleteQueries[0];
            Execute_query_response response = DatabaseService.ExecuteSql(execute_query, "delete");
            if (!string.IsNullOrEmpty(response.Error))
            {
                writeToLog.WriteErrorLog($"SQL Query: {execute_query.Query}");
                writeToLog.WriteErrorLog($"Error: {response.Error}");
                writeToLog.WriteErrorLog("DELETE fail_pin_rate_list_pin_ball error");
            }
            execute_query = deleteQueries[1];
            response = DatabaseService.ExecuteSql(execute_query, "delete");
            if (!string.IsNullOrEmpty(response.Error))
            {
                writeToLog.WriteErrorLog($"SQL Query: {execute_query.Query}");
                writeToLog.WriteErrorLog($"Error: {response.Error}");
                writeToLog.WriteErrorLog("DELETE fail_pin_rate_test_result error ");
            }
            execute_query = deleteQueries[2];
            response = DatabaseService.ExecuteSql(execute_query, "delete");
            if (!string.IsNullOrEmpty(response.Error))
            {
                writeToLog.WriteErrorLog($"SQL Query: {execute_query.Query}");
                writeToLog.WriteErrorLog($"Error: {response.Error}");
                writeToLog.WriteErrorLog("DELETE fail_pin_rate_list and fail_pin_rate_info error");
            }
            return response;
        }

        internal static Execute_query[] BuildDeleteFailPinLogQueries(string failPinId)
        {
            return new[]
            {
                new Execute_query
                {
                    Query = @"DELETE t3
										FROM fail_pin_rate_list_pin_ball t3
										LEFT JOIN fail_pin_rate_list t2 ON t2.id = t3.fail_pin_rate_list_id
									WHERE t2.fail_pin_rate_info_id = @failPinId; ",
                    Parameters = new { failPinId }
                },
                new Execute_query
                {
                    Query = @"DELETE t4
										FROM fail_pin_rate_test_result t4
										LEFT JOIN fail_pin_rate_list t2 ON t2.id = t4.fail_pin_rate_list_id
									WHERE t2.fail_pin_rate_info_id = @failPinId; ",
                    Parameters = new { failPinId }
                },
                new Execute_query
                {
                    Query = @"DELETE t1, t2
										  FROM fail_pin_rate_list t2
										  LEFT JOIN fail_pin_rate_info t1 ON t1.id = t2.fail_pin_rate_info_id
									WHERE t1.id = @failPinId; ",
                    Parameters = new { failPinId }
                }
            };
        }

        public void AddColumnForDataset(DataSet ds_lot_statistic, List<StatisticItem> values)
        {
            for (int i = 0; i < ds_lot_statistic.Tables.Count; i++)
            {
                ds_lot_statistic.Tables[i].Columns.Add("avg_2", typeof(double));
                ds_lot_statistic.Tables[i].Columns.Add("pass_n", typeof(int));
                if (ds_lot_statistic.Tables[i].Rows.Count < 1) continue;
                ds_lot_statistic.Tables[i].Rows[0]["AVG"] = Math.Round(values[i].avg, 9);
                ds_lot_statistic.Tables[i].Rows[0]["avg_2"] = Math.Round(values[i].avg2, 9);
                ds_lot_statistic.Tables[i].Rows[0]["pass_n"] = values[i].pass_n;
            }
        }
        /// <summary>
        /// 處理輸入字串，進行空值檢查及取代成預設值
        /// </summary>
        /// <param name="input">輸入字串</param>
        /// <param name="defaultValue">預設值，預設為"No Data"</param>
        /// <returns>處理後的字串</returns>
        public string ConvertEmptyToDefaultString(string inputValue, string defaultValue = "No Data")
        {
            if (string.IsNullOrEmpty(inputValue?.Trim()))
            {
                return defaultValue;
            }
            return inputValue.Trim();
        }
        /// <summary>
        /// 驗證並轉換統計參數，若為非數字字串則轉為 0
        /// </summary>
        /// <param name="value">待驗證的值</param>
        /// <param name="columnName">欄位名稱（用於日誌記錄）</param>
        /// <param name="itemName">項目名稱（用於日誌記錄）</param>
        /// <returns>轉換後的 decimal 值</returns>
        private double ValidateAndConvertStatisticValue(string value, string columnName = "", string itemName = "")
        {
            try
            {
                // 檢查輸入是否為空或 null
                if (string.IsNullOrWhiteSpace(value))
                {
                    writeToLog.WriteInfoLog(string.Format("統計項目 [{0}] 欄位 [{1}] 值為空，轉換為 0", itemName, columnName));
                    return 0;
                }
                string trimmedValue = value.Trim();
                // 嘗試將字串轉換為 double
                if (double.TryParse(trimmedValue, out double result))
                {
                    return result;
                }
                // 轉換失敗，記錄日誌並返回 0
                writeToLog.WriteInfoLog(string.Format("統計項目 [{0}] 欄位 [{1}] 值 [{2}] 無法轉換為有效數字，轉換為 0", itemName, columnName, trimmedValue));
                return 0;
            }
            catch (Exception ex)
            {
                writeToLog.WriteErrorLog(string.Format("統計項目 [{0}] 欄位 [{1}] 值 [{2}] 驗證過程發生異常：{3}，轉換為 0", itemName, columnName, value, ex.Message));
                return 0;
            }
        }
    }
}
