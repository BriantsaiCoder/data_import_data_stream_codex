using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using DCT_data_import.Common;
using static DCT_data_import.DbObject;

namespace DCT_data_import
{
    /// <summary>
    /// 重構後的檔案處理類別
    /// 使用共用模組來減少重複程式碼並提升維護性
    /// </summary>
    public class FileProcessRefactored
    {
        private readonly WriteToLog _writeToLog;
        private readonly DatabaseHelper _databaseHelper;
        private readonly FileValidator _fileValidator;

        public FileProcessRefactored()
        {
            _writeToLog = new WriteToLog();
            _databaseHelper = new DatabaseHelper();
            _fileValidator = new FileValidator();
        }

        /// <summary>
        /// 檢查 DB Key 是否存在於資料庫
        /// </summary>
        public bool IsDBKeyExistInDB(string tableName, string dbKey, DatabaseService databaseService)
        {
            try
            {
                // 確保資料庫和相關資料表存在
                bool databaseExists = databaseService.EnsureDatabaseExistsAsync(Program.DATABASE).GetAwaiter().GetResult();
                bool tableExists = databaseService.EnsureTableExistsAsync(tableName).GetAwaiter().GetResult();

                if (!databaseExists || !tableExists)
                {
                    _writeToLog.WriteToDataImportLog(string.Format("無法確保資料庫或資料表 {0} 存在", tableName));
                    return false;
                }

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                var query = string.Format("SELECT db_key FROM {0} WHERE db_key='{1}';", tableName, dbKey);
                var executeQuery = new Execute_query { Query = query };
                
                Execute_query_response response = databaseService.ExecuteSqlAsync(executeQuery).GetAwaiter().GetResult();
                
                if (!string.IsNullOrEmpty(response.Error))
                {
                    _writeToLog.WriteToDataImportLog(string.Format("SQL Query: {0}", executeQuery.Query));
                    _writeToLog.WriteToDataImportLog(string.Format("Error: {0}", response.Error));
                    return false;
                }

                stopwatch.Stop();
                return response.Data != null && response.Data.Count > 0;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("檢查DB Key存在性時發生錯誤: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Recovery Rate 匯入資料庫 - 重構版本
        /// </summary>
        public bool ImportRecoveryData(RecoveryRateDataContentFormat content, DatabaseService databaseService)
        {
            try
            {
                if (content == null || content.FinalRecoveryRateTable == null || content.FinalRecoveryRateTable.Rows.Count < 1)
                {
                    _writeToLog.WriteToDataImportLog("Recovery Rate 資料為空，無法匯入");
                    return false;
                }

                // 使用 DatabaseHelper 轉換 DataTable 為 SQL
                var sqlResult = _databaseHelper.ConvertDataTableToSql(content.FinalRecoveryRateTable);
                
                if (string.IsNullOrEmpty(sqlResult.Columns) || sqlResult.ValuesList.Count == 0)
                {
                    _writeToLog.WriteToDataImportLog("無法生成有效的 SQL 語句");
                    return false;
                }

                // 批次處理資料
                const int batchSize = 5000;
                var totalValues = sqlResult.ValuesList;
                
                for (int i = 0; i < totalValues.Count; i += batchSize)
                {
                    var batchValues = totalValues.Skip(i).Take(batchSize).ToList();
                    var values = string.Join(",", batchValues);
                    
                    var response = ExecuteInsertWithAPI(databaseService, "recovery_rate", sqlResult.Columns, values);
                    
                    if (!string.IsNullOrEmpty(response.Error))
                    {
                        _writeToLog.WriteToDataImportLog(string.Format("INSERT INTO recovery_rate error: {0}", response.Error));
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("ImportRecoveryData 發生錯誤: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 執行 INSERT 操作 - 使用 API
        /// </summary>
        public Execute_query_response ExecuteInsertWithAPI(DatabaseService databaseService, string tableName, string columns, string values)
        {
            try
            {
                // 確保資料庫和相關資料表存在
                bool databaseExists = databaseService.EnsureDatabaseExistsAsync(Program.DATABASE).GetAwaiter().GetResult();
                bool tableExists = databaseService.EnsureTableExistsAsync(tableName).GetAwaiter().GetResult();

                if (!databaseExists || !tableExists)
                {
                    _writeToLog.WriteToDataImportLog(string.Format("無法確保資料庫或資料表 {0} 存在", tableName));
                    return new Execute_query_response { Error = string.Format("Database or table {0} does not exist", tableName) };
                }

                var sql = _databaseHelper.BuildInsertSql(tableName, columns, values);
                var executeQuery = new Execute_query { Query = sql };

                Execute_query_response response = databaseService.ExecuteSqlAsync(executeQuery, "insert").GetAwaiter().GetResult();
                
                if (!string.IsNullOrEmpty(response.Error))
                {
                    _writeToLog.WriteToDataImportLog(string.Format("SQL Query: {0}", executeQuery.Query));
                    _writeToLog.WriteToDataImportLog(string.Format("Error: {0}", response.Error));
                    _writeToLog.WriteToDataImportLog(string.Format("INSERT {0} error", tableName));
                }

                return response;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog(string.Format("INSERT {0} error: {1}", tableName, ex.Message));
                return new Execute_query_response { Error = ex.Message };
            }
        }
    }
}