using System;
using System.Globalization;
using System.Linq;

namespace DCT_data_import.Common
{
    /// <summary>
    /// 字串處理共用模組
    /// 統一處理所有字串相關操作
    /// </summary>
    public static class StringHelper
    {
        /// <summary>
        /// 處理輸入字串，進行空值檢查及取代成預設值
        /// </summary>
        /// <param name="inputValue">輸入字串</param>
        /// <param name="defaultValue">預設值，預設為"No Data"</param>
        /// <returns>處理後的字串</returns>
        public static string ConvertEmptyToDefault(string inputValue, string defaultValue = "No Data")
        {
            if (string.IsNullOrEmpty(inputValue?.Trim()))
            {
                return defaultValue;
            }
            return inputValue.Trim();
        }

        /// <summary>
        /// 驗證並格式化日期時間字串
        /// </summary>
        /// <param name="input">輸入的日期時間字串</param>
        /// <param name="format">期望的日期格式，預設為 "yyyy-MM-dd HH:mm:ss"</param>
        /// <returns>格式化後的日期時間字串，失敗時回傳當前時間</returns>
        public static string ValidateDateTime(string input, string format = "yyyy-MM-dd HH:mm:ss")
        {
            if (DateTime.TryParseExact(input, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
            {
                return result.ToString(format);
            }
            else
            {
                return DateTime.Now.ToString(format);
            }
        }

        /// <summary>
        /// 自訂日期格式解析器，解析 "Jun_06_2022_12_08_22" 格式
        /// </summary>
        /// <param name="datetime">日期時間字串</param>
        /// <returns>格式化後的日期時間字串</returns>
        public static string CustomizeDateTimeParser(string datetime)
        {
            try
            {
                string[] timeSplit = datetime.Split('_');
                if (timeSplit.Length != 6) return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string newDatetimeStr = $"{timeSplit[0]} {timeSplit[1]} {timeSplit[2]} {timeSplit[3]}:{timeSplit[4]}:{timeSplit[5]}";
                
                if (DateTime.TryParse(newDatetimeStr, out DateTime dateTime))
                {
                    return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
            catch (Exception)
            {
                return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        /// <summary>
        /// 檢查字串是否包含中文字符
        /// </summary>
        /// <param name="input">輸入字串</param>
        /// <returns>是否包含中文字符</returns>
        public static bool ContainsChinese(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;

            // 移除 Unicode BOM 標記
            if (input.StartsWith("\uFEFF"))
            {
                input = input.Substring(1);
            }

            // 檢查是否包含中文字符 (Unicode 範圍 0x4E00-0x9FFF)
            return input.Any(c => c >= 0x4E00 && c <= 0x9FFF);
        }

        /// <summary>
        /// 標準化欄位名稱
        /// 將欄位名稱轉為小寫並處理特殊字符
        /// </summary>
        /// <param name="columnName">原始欄位名稱</param>
        /// <returns>標準化後的欄位名稱</returns>
        public static string NormalizeColumnName(string columnName)
        {
            if (string.IsNullOrEmpty(columnName)) return string.Empty;

            string normalized = columnName.ToLower().Trim().Replace(" ", "_");

            // 處理常見的欄位名稱對應
            switch (normalized)
            {
                case "db key": return "db_key";
                case "os machine": return "os_machine";
                case "ao lot": return "ao_lot";
                case "retestpass": return "re_test_pass";
                case "failpincount": return "fail_pin_count";
                case "recovery rate(%)": return "recovery_rate";
                case "bondingdiagram": return "bonding_diagram";
                case "pass without ocr": return "pass_without_ocr";
                case "open without ocr": return "open_without_ocr";
                case "short & others": return "short_others";
                case "pass without ocr_ppm": return "pass_without_ocr_ppm";
                case "open without ocr_ppm": return "open_without_ocr_ppm";
                case "short & others_ppm": return "short_others_ppm";
                case "prober_/_handler": return "prober/handler";
                case "l/b_id": return "L/B_id";
                case "handler_repair_starttime": return "handler_repair_start_time";
                case "handler_repair_endtime": return "handler_repair_end_time";
                case "#_of_pass": return "pass";
                case "#_of_fail": return "fail";
                case "siteid": return "site_id";
                case "p/f": return "pass/fail";
                case "diff_time_(die)": return "diff_time_die";
                case "end_time_(die)": return "end_time_die";
                case "first_time_(die)": return "first_time_die";
                case "diff_time_(file)": return "diff_time_file";
                case "pass_/_fail": return "pass/fail";
                case "dct_i-v_curve_tool_md5": return "dct_iv_curve_tool_md5";
                case "simplificationui_md5": return "simplification_ui_md5";
                case "autolearn_pui_version": return "auto_learn_pui_version";
                default: return normalized;
            }
        }

        /// <summary>
        /// 安全轉換字串為數值類型
        /// </summary>
        /// <param name="value">輸入字串</param>
        /// <param name="defaultValue">預設值</param>
        /// <returns>轉換後的數值或預設值</returns>
        public static int SafeParseInt(string value, int defaultValue = 0)
        {
            if (int.TryParse(value?.Trim(), out int result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// 安全轉換字串為 decimal 類型
        /// </summary>
        /// <param name="value">輸入字串</param>
        /// <param name="defaultValue">預設值</param>
        /// <returns>轉換後的數值或預設值</returns>
        public static decimal SafeParseDecimal(string value, decimal defaultValue = 0m)
        {
            if (decimal.TryParse(value?.Trim(), out decimal result))
            {
                return result;
            }
            return defaultValue;
        }
    }
}