using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using static DCT_data_import.DbObject;

namespace DCT_data_import.Common
{
    /// <summary>
    /// 資料庫操作共用模組
    /// 統一管理所有資料庫相關操作
    /// </summary>
    public class DatabaseHelper
    {
        private readonly WriteToLog _writeToLog;

        public DatabaseHelper()
        {
            _writeToLog = new WriteToLog();
        }

        /// <summary>
        /// 建立 INSERT SQL 語句
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="columns">欄位名稱字串</param>
        /// <param name="values">值字串</param>
        /// <returns>完整的 INSERT SQL 語句</returns>
        public string BuildInsertSql(string tableName, string columns, string values)
        {
            return string.Format("INSERT INTO {0}({1}) VALUES ({2});", tableName, columns, values);
        }

        /// <summary>
        /// 建立批次 INSERT SQL 語句
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="columns">欄位名稱陣列</param>
        /// <param name="dataRows">資料行集合</param>
        /// <param name="batchSize">批次大小</param>
        /// <returns>SQL 語句陣列</returns>
        public List<string> BuildBatchInsertSql(string tableName, string[] columns, List<object[]> dataRows, int batchSize = 1000)
        {
            var sqlList = new List<string>();
            var columnStr = string.Join(",", columns.Select(c => string.Format("`{0}`", c)));

            for (int i = 0; i < dataRows.Count; i += batchSize)
            {
                var batch = dataRows.Skip(i).Take(batchSize);
                var valuesList = new List<string>();

                foreach (var row in batch)
                {
                    var rowValues = string.Join(",", row.Select(v => string.Format("\"{0}\"", StringHelper.ConvertEmptyToDefault(v?.ToString()))));
                    valuesList.Add(string.Format("({0})", rowValues));
                }

                var sql = string.Format("INSERT INTO {0}({1}) VALUES {2};", tableName, columnStr, string.Join(",", valuesList));
                sqlList.Add(sql);
            }

            return sqlList;
        }

        /// <summary>
        /// 建立 UPDATE SQL 語句
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="updates">更新的欄位和值</param>
        /// <param name="whereCondition">WHERE 條件</param>
        /// <returns>UPDATE SQL 語句</returns>
        public string BuildUpdateSql(string tableName, Dictionary<string, object> updates, string whereCondition)
        {
            var setClause = string.Join(",", updates.Select(kv => string.Format("`{0}` = \"{1}\"", kv.Key, kv.Value)));
            return string.Format("UPDATE {0} SET {1} WHERE {2};", tableName, setClause, whereCondition);
        }

        /// <summary>
        /// 建立 DELETE SQL 語句
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="whereCondition">WHERE 條件</param>
        /// <returns>DELETE SQL 語句</returns>
        public string BuildDeleteSql(string tableName, string whereCondition)
        {
            return string.Format("DELETE FROM {0} WHERE {1};", tableName, whereCondition);
        }

        /// <summary>
        /// DataTable 轉 SQL 結果類別
        /// </summary>
        public class DataTableToSqlResult
        {
            public string Columns { get; set; }
            public List<string> ValuesList { get; set; }

            public DataTableToSqlResult()
            {
                ValuesList = new List<string>();
            }
        }

        /// <summary>
        /// 將 DataTable 轉換為 SQL INSERT 語句的欄位和值
        /// </summary>
        /// <param name="dataTable">資料表</param>
        /// <param name="normalizeColumnNames">是否標準化欄位名稱</param>
        /// <returns>欄位名稱和值的集合</returns>
        public DataTableToSqlResult ConvertDataTableToSql(DataTable dataTable, bool normalizeColumnNames = true)
        {
            var result = new DataTableToSqlResult();
            
            if (dataTable == null || dataTable.Rows.Count == 0)
                return result;

            // 處理欄位名稱
            var columnNames = new List<string>();
            foreach (DataColumn column in dataTable.Columns)
            {
                string columnName = normalizeColumnNames 
                    ? StringHelper.NormalizeColumnName(column.ColumnName) 
                    : column.ColumnName.ToLower();
                columnNames.Add(string.Format("`{0}`", columnName));
            }

            result.Columns = string.Join(",", columnNames);

            // 處理資料行
            foreach (DataRow row in dataTable.Rows)
            {
                var values = new List<string>();
                for (int i = 0; i < dataTable.Columns.Count; i++)
                {
                    string value = StringHelper.ConvertEmptyToDefault(row[i]?.ToString());
                    
                    // 特殊處理日期欄位
                    if (dataTable.Columns[i].ColumnName.ToLower().Contains("date") ||
                        dataTable.Columns[i].ColumnName.ToLower().Contains("time"))
                    {
                        if (DateTime.TryParse(value, out DateTime dateValue))
                        {
                            value = dateValue.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                    }

                    values.Add(string.Format("\"{0}\"", value));
                }
                result.ValuesList.Add(string.Format("({0})", string.Join(",", values)));
            }

            return result;
        }

        /// <summary>
        /// 驗證資料表欄位
        /// </summary>
        /// <param name="dataTable">要驗證的資料表</param>
        /// <param name="requiredColumns">必要欄位陣列</param>
        /// <returns>驗證結果</returns>
        public bool ValidateDataTableColumns(DataTable dataTable, string[] requiredColumns)
        {
            if (dataTable == null || requiredColumns == null) return false;

            var tableColumns = new HashSet<string>(
                dataTable.Columns.Cast<DataColumn>()
                    .Select(c => c.ColumnName.ToLower())
            );

            return requiredColumns.All(col => tableColumns.Contains(col.ToLower()));
        }

        /// <summary>
        /// 執行批次資料庫操作
        /// </summary>
        /// <param name="databaseService">資料庫服務</param>
        /// <param name="sqlCommands">SQL 命令列表</param>
        /// <param name="operationType">操作類型</param>
        /// <returns>執行結果</returns>
        public async System.Threading.Tasks.Task<Execute_query_response> ExecuteBatchOperationsAsync(
            DatabaseService databaseService, 
            List<string> sqlCommands, 
            string operationType = "insert")
        {
            Execute_query_response lastResponse = null;

            try
            {
                foreach (var sql in sqlCommands)
                {
                    var execute_query = new Execute_query { Query = sql };
                    lastResponse = await databaseService.ExecuteSqlAsync(execute_query, operationType);

                    if (!string.IsNullOrEmpty(lastResponse.Error))
                    {
                        _writeToLog.WriteToDataImportLog($"批次操作失敗 - SQL: {sql}");
                        _writeToLog.WriteToDataImportLog($"錯誤: {lastResponse.Error}");
                        return lastResponse;
                    }
                }

                return lastResponse;
            }
            catch (Exception ex)
            {
                _writeToLog.WriteToDataImportLog($"批次操作發生異常: {ex.Message}");
                return new Execute_query_response { Error = ex.Message };
            }
        }

        /// <summary>
        /// 組建標準的資料表欄位映射
        /// </summary>
        /// <param name="sourceColumns">來源欄位陣列</param>
        /// <param name="mappingRules">欄位映射規則</param>
        /// <returns>映射後的欄位陣列</returns>
        public string[] MapColumns(string[] sourceColumns, Dictionary<string, string> mappingRules = null)
        {
            if (sourceColumns == null) return new string[0];

            var result = new string[sourceColumns.Length];
            
            for (int i = 0; i < sourceColumns.Length; i++)
            {
                string column = sourceColumns[i];
                
                // 先檢查是否有自訂映射規則
                if (mappingRules != null && mappingRules.ContainsKey(column))
                {
                    result[i] = mappingRules[column];
                }
                else
                {
                    // 使用標準化處理
                    result[i] = StringHelper.NormalizeColumnName(column);
                }
            }

            return result;
        }

        /// <summary>
        /// 準備數值類型欄位的 SQL 值
        /// </summary>
        /// <param name="value">原始值</param>
        /// <param name="columnName">欄位名稱</param>
        /// <param name="numericColumns">數值類型欄位清單</param>
        /// <returns>SQL 值字串</returns>
        public string PrepareColumnValue(string value, string columnName, string[] numericColumns = null)
        {
            if (string.IsNullOrEmpty(value) || value.Trim() == "NA")
            {
                // 檢查是否為數值類型欄位
                if (numericColumns != null && numericColumns.Contains(columnName.ToLower()))
                {
                    return "NULL";
                }
                return string.Format("\"{0}\"", StringHelper.ConvertEmptyToDefault(value));
            }

            // 處理特殊路徑字符
            if (columnName.ToLower().Contains("path"))
            {
                return string.Format("\"{0}\"", value.Replace(@"\", @"\\"));
            }

            // 處理日期時間欄位
            if (columnName.ToLower().Contains("time") || columnName.ToLower().Contains("date"))
            {
                if (DateTime.TryParse(value, out DateTime dateTime))
                {
                    return string.Format("\"{0}\"", dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                }
                return "NULL";
            }

            return string.Format("\"{0}\"", StringHelper.ConvertEmptyToDefault(value));
        }
    }
}