using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace DCT_data_import.Common
{
    /// <summary>
    /// 檔案驗證共用模組
    /// 統一處理所有檔案格式驗證與欄位檢查
    /// </summary>
    public class FileValidator
    {
        private readonly WriteToLog _writeToLog;

        public FileValidator()
        {
            _writeToLog = new WriteToLog();
        }

        /// <summary>
        /// 檔案驗證結果類別
        /// </summary>
        public class FileValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
            public List<string> MissingColumns { get; set; }
            public List<string> ExtraColumns { get; set; }
            public int TotalRows { get; set; }
            public int ValidRows { get; set; }

            public FileValidationResult()
            {
                MissingColumns = new List<string>();
                ExtraColumns = new List<string>();
            }
        }

        /// <summary>
        /// 驗證檔案是否存在且可讀取
        /// </summary>
        /// <param name="filePath">檔案路徑</param>
        /// <returns>驗證結果</returns>
        public FileValidationResult ValidateFileAccess(string filePath)
        {
            var result = new FileValidationResult();

            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "檔案路徑不能為空";
                    return result;
                }

                if (!File.Exists(filePath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = string.Format("檔案不存在: {0}", filePath);
                    return result;
                }

                using (var stream = File.OpenRead(filePath))
                {
                    result.IsValid = true;
                    result.ErrorMessage = string.Empty;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = string.Format("檔案驗證發生錯誤: {0}", ex.Message);
                _writeToLog.WriteToDataImportLog(result.ErrorMessage);
            }

            return result;
        }

        /// <summary>
        /// 驗證 DataTable 結構是否符合要求
        /// </summary>
        /// <param name="dataTable">要驗證的 DataTable</param>
        /// <param name="requiredColumns">必要欄位陣列</param>
        /// <param name="normalizeColumnNames">是否標準化欄位名稱</param>
        /// <returns>驗證結果</returns>
        public FileValidationResult ValidateDataTableStructure(
            DataTable dataTable, 
            string[] requiredColumns, 
            bool normalizeColumnNames = true)
        {
            var result = new FileValidationResult();

            try
            {
                if (dataTable == null)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "DataTable 不能為 null";
                    return result;
                }

                if (requiredColumns == null || requiredColumns.Length == 0)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "必要欄位清單不能為空";
                    return result;
                }

                var actualColumns = new HashSet<string>();
                foreach (DataColumn column in dataTable.Columns)
                {
                    string columnName = normalizeColumnNames
                        ? StringHelper.NormalizeColumnName(column.ColumnName)
                        : column.ColumnName.ToLower();
                    actualColumns.Add(columnName);
                }

                var normalizedRequired = new HashSet<string>(
                    requiredColumns.Select(col => normalizeColumnNames 
                        ? StringHelper.NormalizeColumnName(col) 
                        : col.ToLower())
                );

                foreach (var requiredCol in normalizedRequired)
                {
                    if (!actualColumns.Contains(requiredCol))
                    {
                        result.MissingColumns.Add(requiredCol);
                    }
                }

                result.TotalRows = dataTable.Rows.Count;
                result.ValidRows = dataTable.Rows.Count;
                result.IsValid = result.MissingColumns.Count == 0;
                
                if (!result.IsValid)
                {
                    result.ErrorMessage = string.Format("缺少必要欄位: {0}", 
                        string.Join(", ", result.MissingColumns));
                }
                else
                {
                    result.ErrorMessage = string.Empty;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = string.Format("DataTable結構驗證發生錯誤: {0}", ex.Message);
                _writeToLog.WriteToDataImportLog(result.ErrorMessage);
            }

            return result;
        }

        /// <summary>
        /// 驗證 DataTable 中的資料內容
        /// </summary>
        /// <param name="dataTable">要驗證的 DataTable</param>
        /// <param name="mandatoryColumns">必須有值的欄位</param>
        /// <param name="numericColumns">數值類型欄位</param>
        /// <param name="dateColumns">日期類型欄位</param>
        /// <returns>驗證結果</returns>
        public FileValidationResult ValidateDataTableContent(
            DataTable dataTable,
            string[] mandatoryColumns = null,
            string[] numericColumns = null,
            string[] dateColumns = null)
        {
            var result = new FileValidationResult();
            var errors = new List<string>();

            try
            {
                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "DataTable 為空或無資料";
                    return result;
                }

                result.TotalRows = dataTable.Rows.Count;
                int validRowCount = 0;

                // 檢查每一行資料
                for (int rowIndex = 0; rowIndex < dataTable.Rows.Count; rowIndex++)
                {
                    var row = dataTable.Rows[rowIndex];
                    bool isRowValid = true;

                    // 檢查必要欄位不能為空
                    if (mandatoryColumns != null)
                    {
                        foreach (var colName in mandatoryColumns)
                        {
                            if (dataTable.Columns.Contains(colName))
                            {
                                var value = row[colName]?.ToString()?.Trim();
                                if (string.IsNullOrEmpty(value))
                                {
                                    errors.Add(string.Format("第{0}行欄位'{1}'不能為空", rowIndex + 1, colName));
                                    isRowValid = false;
                                }
                            }
                        }
                    }

                    // 檢查數值欄位格式
                    if (numericColumns != null)
                    {
                        foreach (var colName in numericColumns)
                        {
                            if (dataTable.Columns.Contains(colName))
                            {
                                var value = row[colName]?.ToString()?.Trim();
                                if (!string.IsNullOrEmpty(value) && value != "NA")
                                {
                                    if (!decimal.TryParse(value, out decimal numValue))
                                    {
                                        errors.Add(string.Format("第{0}行欄位'{1}'數值格式錯誤: {2}", rowIndex + 1, colName, value));
                                        isRowValid = false;
                                    }
                                }
                            }
                        }
                    }

                    // 檢查日期欄位格式
                    if (dateColumns != null)
                    {
                        foreach (var colName in dateColumns)
                        {
                            if (dataTable.Columns.Contains(colName))
                            {
                                var value = row[colName]?.ToString()?.Trim();
                                if (!string.IsNullOrEmpty(value) && value != "NA")
                                {
                                    if (!DateTime.TryParse(value, out DateTime dateValue))
                                    {
                                        errors.Add(string.Format("第{0}行欄位'{1}'日期格式錯誤: {2}", rowIndex + 1, colName, value));
                                        isRowValid = false;
                                    }
                                }
                            }
                        }
                    }

                    if (isRowValid)
                    {
                        validRowCount++;
                    }
                }

                result.ValidRows = validRowCount;
                result.IsValid = errors.Count == 0;
                result.ErrorMessage = errors.Count > 0 ? string.Join("; ", errors) : string.Empty;

                // 記錄驗證結果
                if (!result.IsValid)
                {
                    _writeToLog.WriteToDataImportLog(string.Format("資料內容驗證失敗: {0}/{1} 行有效", validRowCount, result.TotalRows));
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = string.Format("資料內容驗證發生錯誤: {0}", ex.Message);
                _writeToLog.WriteToDataImportLog(result.ErrorMessage);
            }

            return result;
        }

        /// <summary>
        /// 取得標準化的欄位名稱對應表
        /// </summary>
        /// <param name="dataTable">原始 DataTable</param>
        /// <returns>欄位名稱對應字典</returns>
        public Dictionary<string, string> GetColumnMapping(DataTable dataTable)
        {
            var mapping = new Dictionary<string, string>();

            if (dataTable != null && dataTable.Columns != null)
            {
                foreach (DataColumn column in dataTable.Columns)
                {
                    string originalName = column.ColumnName;
                    string normalizedName = StringHelper.NormalizeColumnName(originalName);
                    mapping[originalName] = normalizedName;
                }
            }

            return mapping;
        }

        /// <summary>
        /// 檢查 DataTable 是否有特定欄位
        /// </summary>
        /// <param name="dataTable">要檢查的 DataTable</param>
        /// <param name="columnNames">欄位名稱陣列</param>
        /// <returns>是否包含所有欄位</returns>
        public bool CheckDataTableColumns(DataTable dataTable, params string[] columnNames)
        {
            if (dataTable == null || columnNames == null) return false;

            foreach (string columnName in columnNames)
            {
                bool found = false;
                foreach (DataColumn column in dataTable.Columns)
                {
                    if (string.Equals(column.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }
    }
}