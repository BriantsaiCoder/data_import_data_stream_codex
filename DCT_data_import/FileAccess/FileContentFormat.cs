using System;
using System.Data;
using System.Linq;
namespace DCT_data_import
{
    public class RecoveryRateDataContentFormat
    {
        private readonly string[] _infoColumns = { "DB Key", "Area", "Factory", "OS Machine", "Customer", "Program", "AO Lot", "Mode", "Date" };
        private readonly string[] _recoveryRateColumns = { "Test_Item", "Defect_mode", "reTestPass", "FailPinCount", "Total_Unit", "Recovery rate(%)" };
        public string ErrMsg { get; set; }
        public DataTable LotInfo { get; set; }
        public DataTable LotRecoveryRate { get; set; }
        public DataTable FinalRecoveryRateTable { get; set; }
        public RecoveryRateDataContentFormat(string errMsg = "")
        {
            ErrMsg = errMsg;
            LotInfo = new DataTable();
            // 因info只有一列資料，故先建立一個空DataRow
            DataRow dr = LotInfo.NewRow();
            LotInfo.Rows.Add(dr);
            LotRecoveryRate = new DataTable();
            FinalRecoveryRateTable = new DataTable();
        }
        // 比對 infoColumns 與 lotInfo 的欄位
        public bool CompareInfo()
        {
            try
            {
                if (LotInfo.Rows.Count < 1) return false;
                string[] columnNames = LotInfo.Columns.Cast<DataColumn>()
                                     .Select(x => x.ColumnName)
                                     .ToArray();
                bool result = true; // infoColumns.SequenceEqual(columnNames);
                for (int i = 0; i < LotInfo.Columns.Count; i++)
                {
                    if (!_infoColumns.Contains(LotInfo.Columns[i].ColumnName))
                    {
                        return false;
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                var writeToLog = new WriteToLog();
                writeToLog.WriteErrorLog($"[RecoveryRateDataContentFormat.CompareInfo] 比對欄位失敗: {ex.Message}");
                Console.WriteLine($"[RecoveryRateDataContentFormat] 比對欄位失敗: {ex.Message}");
                return false;
            }
        }
        // 比對 recoveryRateColumns 與 LotRecoveryRate 的欄位
        public bool CompareRecoveryRate()
        {
            try
            {
                if (LotRecoveryRate.Rows.Count < 1) return false;
                string[] columnNames = LotRecoveryRate.Columns.Cast<DataColumn>()
                                     .Select(x => x.ColumnName)
                                     .ToArray();
                bool result = true; // recoveryRateColumns.SequenceEqual(columnNames);
                for (int i = 0; i < LotRecoveryRate.Columns.Count; i++)
                {
                    if (!_recoveryRateColumns.Contains(LotRecoveryRate.Columns[i].ColumnName))
                    {
                        return false;
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                var writeToLog = new WriteToLog();
                writeToLog.WriteErrorLog($"[RecoveryRateDataContentFormat.CompareRecoveryRate] 比對欄位失敗: {ex.Message}");
                Console.WriteLine($"[RecoveryRateDataContentFormat] 比對Recovery Rate欄位失敗: {ex.Message}");
                return false;
            }
        }
    }
    public class RawDataContentFormat
    {
        private readonly string[] _infoColumns = { "Version", "Mac_Address", "DB_Key", "Customer", "Package", "BondingDiagram", "Program", "Device",
            "Control_lot", "AO_lot", "OS_Machine_ID", "OS_Test_Board_ID", "User_ID", "Schedule_Lot", "File_Name", "Yield(%)", "TOTAL", "PASS",
            "OPEN_PIN_FAIL", "SHORT_PIN_FAIL", "LEAKAGE_PIN_FAIL", "TOTAL_PPM", "OPEN_PIN_FAIL_PPM", "SHORT_PIN_FAIL_PPM",
            "LEAKAGE_PIN_FAIL_PPM", "Total_Test_Items", "Average_Test_Time", "Clear_Count", "Start", "Stop",
            "Pass", "Pass without OCR","OPEN","OPEN without OCR","Short & Others","Pass without OCR_PPM","OPEN_PPM","OPEN without OCR_PPM","Short & Others_PPM" };
        private readonly string[] _statisticColumns = { "Item No", "Item Name", "net_name", "Force", "Wait time", "Spec MAX", "Spec MIN", "# of PASS", "# of FAIL", "MIN", "MAX", "AVG",
            "STDEV", "Cp", "Cpk", "Ppk", "unit", "value" };
        public string ErrMsg { get; set; }
        public DataTable LotInfo { get; set; }
        public DataSet LotStatistic { get; set; }
        public DataTable LotResult { get; set; }
        public RawDataContentFormat(string errMsg = "")
        {
            ErrMsg = errMsg;
            LotInfo = new DataTable();
            // 因info只有一列資料，故先建立一個空DataRow
            DataRow dr = LotInfo.NewRow();
            LotInfo.Rows.Add(dr);
            LotStatistic = new DataSet();
            LotResult = new DataTable();
        }
        // 比對 infoColumns 與 lotInfo 的欄位
        public bool CompareInfo()
        {
            if (LotInfo.Rows.Count < 1) return false;
            string[] columnNames = LotInfo.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();
            bool result = true; // infoColumns.SequenceEqual(columnNames);
            for (int i = 0; i < LotInfo.Columns.Count; i++)
            {
                if (!_infoColumns.Contains(LotInfo.Columns[i].ColumnName))
                {
                    return false;
                }
            }
            return result;
        }
        // 比對 statisticColumns 與 lotStatistic 的欄位
        public bool CompareStatistic()
        {
            if (LotStatistic.Tables.Count < 1) return false;
            string[] columnNames = LotStatistic.Tables[0].Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();
            bool result = true; // statisticColumns.SequenceEqual(columnNames);
            for (int i = 0; i < LotStatistic.Tables[0].Columns.Count; i++)
            {
                //Console.WriteLine(statisticColumns[i] +" vs " + this.lotStatistic.Tables[0].Columns[i]);
                if (!_statisticColumns.Contains(LotStatistic.Tables[0].Columns[i].ColumnName))
                {
                    return false;
                }
            }
            return result;
        }
    }
    public class TestStatusContentFormat
    {
        private readonly string[] _infoColumns = { "DB_Key", "Mac_Address", "IP_Address", "Area", "Factory", "Machine Type", "Machine ID", "Customer",
            "Device Production", "Device Engineer", "Test Program", "Program_path", "Lot ID", "Wafer ID", "Execution mode", "Prober / Handler",
            "L/B ID", "Dut board type", "Efficiency check", "UI Flow checksum", "Yield", "File type", " Start Time", "End Time", "Lead_count", "Site_qty",
            "BD_Leak", "PG_Leak", "Wireclose_Leak",
            "handler_type","handler_sw_version","handler_repair_startTime","handler_repair_endTime","DOE_flag","HSO_mode","MP_API_log" ,"MP_TT_log","Smart_Delay_enable","Smart_Delay_time","ATV_Information",
            "NetlistInfo","TP_CheckerDetectionResults","PG_LeakageEnabled","LeakageEnabled","EnhanceTestTtemQTY","First_Yield","shortFailAnalysisFlag","OSVersion","DCT_Type","DCT_Qty", "DCT_CH_Qty","LB_Type","ConnecterType","Short_Plate_Check_Status","Short_Plate_Check_Pin_qty_match","TP_HighRiskLot","TP_WarningLot","TP_OverkillQTY"};
        private readonly string[] _statusColumns = { "DPW", "Duts", "CSV Name", "UPH", "Avg test time", "Max test time", "Min test time", "Avg index test time",
            "Max index test time", "Min index test time", "Diff time (die)", "End time (die)", "First time (die)", "Diff time (file)", "Conclusion file path",
            "Raw date file path", "S2S diff file path", "PASS / FAIL", "Case A Result", "Case B Result", "Case C Result", "PUI result", "PUI respond",
            "PUI file type", "PHI result", "PHI respond", "PHI file type", "TP result", "TP respond", "manual_data_module_csv_g result",
            "manual_data_module_csv_g respond", "data_module_stdf_g result", "data_module_stdf_g respond", "data_module_txt_g result",
            "data_module_txt_g respond", "data_module_std_g result", "data_module_std_g respond", "Test_Time_Module_csv_g result",
            "Test_Time_Module_csv_g respond", "data_module_act_smart1_txt result", "data_module_act_smart1_txt respond",
            "data_module_ASEKH_smart1_xml result", "data_module_ASEKH_smart1_xml respond", "data_module_act_fail_log result",
            "data_module_act_fail_log respond", "VIM result", "VIM respond", "VIM_open result", "VIM_open respond", "ViCbit result", "ViCbit respond",
            "ViCbit_open result", "ViCbit_open respond", "Pattern result", "Pattern respond" };
        public string ErrMsg { get; set; }
        public DataTable Tester_device_info { get; set; }
        public DataTable Tester_status { get; set; }
        public DataTable Tester_sw_version { get; set; }
        public DataTable Tester_production_analysis { get; set; }
        public TestStatusContentFormat(string errMsg = "")
        {
            ErrMsg = errMsg;
            Tester_device_info = new DataTable();
            Tester_status = new DataTable();
            Tester_sw_version = new DataTable();
            Tester_production_analysis = new DataTable();
        }
        // 比對 infoColumns 與 tester_device_info 的欄位
        public bool CompareInfo()
        {
            if (Tester_device_info.Rows.Count < 1) return false;
            string[] columnNames = Tester_device_info.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();
            bool result = true; // infoColumns.SequenceEqual(columnNames);
            for (int i = 0; i < Tester_device_info.Columns.Count; i++)
            {
                if (!_infoColumns.Contains(Tester_device_info.Columns[i].ColumnName))
                {
                    return false;
                }
            }
            return result;
        }
        // 比對 statusColumns 與 tester_status 的欄位
        public bool CompareStatus()
        {
            if (Tester_status.Rows.Count < 1) return false;
            string[] columnNames = Tester_status.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();
            bool result = true; // infoColumns.SequenceEqual(columnNames);
            for (int i = 0; i < Tester_status.Columns.Count; i++)
            {
                if (!_statusColumns.Contains(Tester_status.Columns[i].ColumnName))
                {
                    return false;
                }
            }
            return result;
        }
    }
    public class UIStatusContentFormat
    {
        private readonly string[] _uiStatusColumns = { "Mac_Address", "Area", "Factory", "OS_Machine", "Date", "Auto_learn", "DCT_Product_File_Setting_UI",
            "DCT_login_UI", "OS_self_diag_2K", "Pattonkan_UI", "DCT_I_V_Curve_Tool", "OS_TESTER_100mA_VI", "OS_TESTER_2A_VI", "OS_Tester_LCR_meter",
            "Wire_assignment_tool", "BGA_highlight_tool", "SimplificationUI", "OS_scan_tool", "DCT_UploadTp_UI", "DCT_AutoDownloadTp",
            "DCT_SW_Control_Tool", "DCT_DownloadTp_KH" };
        public string ErrMsg { get; set; }
        public DataTable UI_status { get; set; }
        public UIStatusContentFormat()
        {
            UI_status = new DataTable();
        }
        public bool CompareUiStatus()
        {
            if (UI_status.Rows.Count < 1) return false;
            string[] columnNames = UI_status.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();
            bool result = true; // infoColumns.SequenceEqual(columnNames);
            for (int i = 0; i < UI_status.Columns.Count; i++)
            {
                if (!_uiStatusColumns.Contains(UI_status.Columns[i].ColumnName))
                {
                    return false;
                }
            }
            return result;
        }
    }
    public class FailPinLogContentFormat
    {
        private readonly string[] _infoColumns = { "Mac Address", "DB Key", "Area", "Factory", "OS Machine", "AO Lot", "Mode", "Data format", "File Name", "Date", "Total", "Pass", "Open", "Short", "LK", "nVTEP" };
        public string ErrMsg { get; set; }
        public DataTable Fail_pin_rate_info { get; set; }
        public DataTable Fail_pin_rate_list { get; set; }
        public DataTable Fail_pin_rate_list_pin_ball { get; set; }
        public DataSet Fail_pin_rate_list_test_result { get; set; }
        public FailPinLogContentFormat()
        {
            Fail_pin_rate_info = new DataTable();
            DataRow dr = Fail_pin_rate_info.NewRow();
            Fail_pin_rate_info.Rows.Add(dr);
            Fail_pin_rate_list = new DataTable();
            Fail_pin_rate_list.Columns.Add("dut", typeof(string));
            Fail_pin_rate_list.Columns.Add("site", typeof(string));
            Fail_pin_rate_list.Columns.Add("fail_type", typeof(string));
            Fail_pin_rate_list_pin_ball = new DataTable();
            Fail_pin_rate_list_pin_ball.Columns.Add("fail_pin_rate_list_id", typeof(string));
            Fail_pin_rate_list_pin_ball.Columns.Add("pin", typeof(string));
            Fail_pin_rate_list_pin_ball.Columns.Add("ball", typeof(string));
            Fail_pin_rate_list_pin_ball.Columns.Add("remark", typeof(string));
            Fail_pin_rate_list_test_result = new DataSet();
        }
        public bool CompareInfo()
        {
            if (Fail_pin_rate_info.Rows.Count < 1) return false;
            string[] columnNames = Fail_pin_rate_info.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();
            bool result = true; // infoColumns.SequenceEqual(columnNames);
            for (int i = 0; i < Fail_pin_rate_info.Columns.Count; i++)
            {
                if (!_infoColumns.Contains(Fail_pin_rate_info.Columns[i].ColumnName))
                {
                    return false;
                }
            }
            return result;
        }
    }
    public class IedaDataFormat
    {
        private readonly string[] _titleColumns = { "ase_lot", "lot_id", "sub_lot", "device", "mpw_code", "produce_code", "tester_id", "oper_id", "test_program", "start_time",
            "end_time", "socket_lid", "load_board_id", "bd_file", "package_notch", "sort_stage", "test_site","fd_file","cover_id_side_blade","socket_id","handler_id","device_rev",
            "tsmc_lot_id","assembly_start_date","assembly_end_date"};
        public int[] titleColumnsDataSize = { 30, 30, 2, 32, 4, 6, 8, 8, 50, 19, 19, 12, 12, 20, 1, 1, 8, 20, 20, 20, 20, 10, 12, 11, 11 };
        private readonly string[] _contentColumns = { "title_id", "touch_down", "sw_bin", "vi_result", "site_index", "index_time", "test_time", "re_probing_flag_retest_flag",
            "handler_arm", "temperature", "package_start_time","handler_arm_force", "wafer_id", "wafer_x", "wafer_y",
            "serial_number", "efuse_string_1", "efuse_string_2","efuse_string_3","efuse_string_4","spare_para_1","spare_para_2","spare_para_3","spare_para_4",
            "soft_bin_name","hard_bin_number","hard_bin_name","ocr_laser_mark_qr_code"};
        public int[] contentColumnsDataSize = { 8, 8, 4, 4, 4, 8, 8, 4, 8, 8, 19, 8, 12, 4, 4, 8, 53, 64, 46, 37, 6, 6, 6, 20, 20, 4, 20, 25 };
        public DataTable IedaTitle { get; set; }
        public DataTable IedaContent { get; set; }
        public string ErrMsg { get; set; }
        public IedaDataFormat()
        {
            IedaTitleInit();
            IedaContentInit();
        }
        private void IedaTitleInit()
        {
            IedaTitle = new DataTable();
            for (int i = 0; i < _titleColumns.Length; i++)
            {
                IedaTitle.Columns.Add(_titleColumns[i], typeof(string));
            }
        }
        private void IedaContentInit()
        {
            IedaContent = new DataTable();
            for (int i = 0; i < _contentColumns.Length; i++)
            {
                IedaContent.Columns.Add(_contentColumns[i], typeof(string));
            }
        }
    }
}