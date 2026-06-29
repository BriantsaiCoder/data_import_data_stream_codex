using System.Data;
using DCT_data_import;
using DCT_data_import.FileAccess;
using Xunit;

namespace DCT_data_import.Tests
{
    public class FileContentFormatTests
    {
        [Fact]
        public void CsvColumnNames_DocumentCurrentDbKeyHeaderVariants()
        {
            Assert.Equal("DB_Key", CsvColumnNames.DbKeyUnderscore);
            Assert.Equal("DB Key", CsvColumnNames.DbKeyWithSpace);
            Assert.NotEqual(CsvColumnNames.DbKeyUnderscore, CsvColumnNames.DbKeyWithSpace);
        }

        [Fact]
        public void CompareMethods_WhenFullKnownColumnsHaveRows_ReturnTrue()
        {
            var recovery = new RecoveryRateDataContentFormat();
            AddColumnsAndRow(recovery.LotInfo,
                CsvColumnNames.DbKeyWithSpace, "Area", "Factory", "OS Machine", "Customer", "Program", "AO Lot", "Mode", "Date");
            AddColumnsAndRow(recovery.LotRecoveryRate,
                "Test_Item", "Defect_mode", "reTestPass", "FailPinCount", "Total_Unit", "Recovery rate(%)");

            var rawData = new RawDataContentFormat();
            AddColumnsAndRow(rawData.LotInfo,
                "Version", "Mac_Address", CsvColumnNames.DbKeyUnderscore, "Customer", "Package", "BondingDiagram", "Program", "Device",
                "Control_lot", "AO_lot", "OS_Machine_ID", "OS_Test_Board_ID", "User_ID", "Schedule_Lot", "File_Name",
                "Yield(%)", "TOTAL", "PASS", "OPEN_PIN_FAIL", "SHORT_PIN_FAIL", "LEAKAGE_PIN_FAIL", "nVTEP_PIN_FAIL",
                "TOTAL_PPM", "OPEN_PIN_FAIL_PPM", "SHORT_PIN_FAIL_PPM", "LEAKAGE_PIN_FAIL_PPM", "nVTEP_PIN_FAIL_PPM",
                "Total_Test_Items", "Average_Test_Time", "Clear_Count", "Start", "Stop", "Pass", "Pass without OCR",
                "OPEN", "OPEN without OCR", "Short & Others", "Pass without OCR_PPM", "OPEN_PPM", "OPEN without OCR_PPM",
                "Short & Others_PPM");
            rawData.LotStatistic.Tables.Add(TableWithRow(
                "Item No", "Item Name", "net_name", "Force", "Wait time", "Spec MAX", "Spec MIN", "# of PASS",
                "# of FAIL", "MIN", "MAX", "AVG", "STDEV", "Cp", "Cpk", "Ppk", "unit", "value"));

            var testStatus = new TestStatusContentFormat();
            AddColumnsAndRow(testStatus.Tester_device_info,
                CsvColumnNames.DbKeyUnderscore, "Mac_Address", "IP_Address", "Area", "Factory", "Machine Type", "Machine ID", "Customer",
                "Device Production", "Device Engineer", "Test Program", "Program_path", "Lot ID", "Wafer ID",
                "Execution mode", "Prober / Handler", "L/B ID", "Dut board type", "Efficiency check",
                "UI Flow checksum", "Yield", "File type", " Start Time", "End Time", "Lead_count", "Site_qty",
                "BD_Leak", "PG_Leak", "Wireclose_Leak", "handler_type", "handler_sw_version",
                "handler_repair_startTime", "handler_repair_endTime", "DOE_flag", "HSO_mode", "MP_API_log",
                "MP_TT_log", "Smart_Delay_enable", "Smart_Delay_time", "ATV_Information", "NetlistInfo",
                "TP_CheckerDetectionResults", "PG_LeakageEnabled", "LeakageEnabled", "EnhanceTestTtemQTY",
                "First_Yield", "shortFailAnalysisFlag", "OSVersion", "DCT_Type", "DCT_Qty", "DCT_CH_Qty",
                "LB_Type", "ConnecterType", "Short_Plate_Check_Status", "Short_Plate_Check_Pin_qty_match",
                "TP_HighRiskLot", "TP_WarningLot", "TP_OverkillQTY");
            AddColumnsAndRow(testStatus.Tester_status,
                "DPW", "Duts", "CSV Name", "UPH", "Avg test time", "Max test time", "Min test time",
                "Avg index test time", "Max index test time", "Min index test time", "Diff time (die)",
                "End time (die)", "First time (die)", "Diff time (file)", "Conclusion file path",
                "Raw date file path", "S2S diff file path", "PASS / FAIL", "Case A Result", "Case B Result",
                "Case C Result", "PUI result", "PUI respond", "PUI file type", "PHI result", "PHI respond",
                "PHI file type", "TP result", "TP respond", "manual_data_module_csv_g result",
                "manual_data_module_csv_g respond", "data_module_stdf_g result", "data_module_stdf_g respond",
                "data_module_txt_g result", "data_module_txt_g respond", "data_module_std_g result",
                "data_module_std_g respond", "Test_Time_Module_csv_g result", "Test_Time_Module_csv_g respond",
                "data_module_act_smart1_txt result", "data_module_act_smart1_txt respond",
                "data_module_ASEKH_smart1_xml result", "data_module_ASEKH_smart1_xml respond",
                "data_module_act_fail_log result", "data_module_act_fail_log respond", "VIM result", "VIM respond",
                "VIM_open result", "VIM_open respond", "ViCbit result", "ViCbit respond", "ViCbit_open result",
                "ViCbit_open respond", "Pattern result", "Pattern respond", "SWT result", "SWT respond");

            var uiStatus = new UIStatusContentFormat();
            AddColumnsAndRow(uiStatus.UI_status,
                "Mac_Address", "Area", "Factory", "OS_Machine", "Date", "Auto_learn", "DCT_Product_File_Setting_UI",
                "DCT_login_UI", "OS_self_diag_2K", "Pattonkan_UI", "DCT_I_V_Curve_Tool", "OS_TESTER_100mA_VI",
                "OS_TESTER_2A_VI", "OS_Tester_LCR_meter", "Wire_assignment_tool", "BGA_highlight_tool",
                "SimplificationUI", "OS_scan_tool", "DCT_UploadTp_UI", "DCT_AutoDownloadTp", "DCT_SW_Control_Tool",
                "DCT_DownloadTp_KH");

            var failPin = new FailPinLogContentFormat();
            AddColumnsAndRow(failPin.Fail_pin_rate_info,
                "Mac Address", CsvColumnNames.DbKeyWithSpace, "Area", "Factory", "OS Machine", "AO Lot", "Mode", "Data format",
                "File Name", "Date", "Total", "Pass", "Open", "Short", "LK", "nVTEP");

            Assert.True(recovery.CompareInfo(), nameof(recovery.CompareInfo));
            Assert.True(recovery.CompareRecoveryRate(), nameof(recovery.CompareRecoveryRate));
            Assert.True(rawData.CompareInfo(), nameof(rawData.CompareInfo));
            Assert.True(rawData.CompareStatistic(), nameof(rawData.CompareStatistic));
            Assert.True(testStatus.CompareInfo(), nameof(testStatus.CompareInfo));
            Assert.True(testStatus.CompareStatus(), nameof(testStatus.CompareStatus));
            Assert.True(uiStatus.CompareUiStatus(), nameof(uiStatus.CompareUiStatus));
            Assert.True(failPin.CompareInfo(), nameof(failPin.CompareInfo));
        }

        [Fact]
        public void CompareMethods_WhenUnexpectedColumnExists_ReturnFalse()
        {
            var recovery = new RecoveryRateDataContentFormat();
            AddColumnsAndRow(recovery.LotInfo, "Unexpected");
            AddColumnsAndRow(recovery.LotRecoveryRate, "Unexpected");

            var rawData = new RawDataContentFormat();
            AddColumnsAndRow(rawData.LotInfo, "Unexpected");
            rawData.LotStatistic.Tables.Add(TableWithRow("Unexpected"));

            var testStatus = new TestStatusContentFormat();
            AddColumnsAndRow(testStatus.Tester_device_info, "Unexpected");
            AddColumnsAndRow(testStatus.Tester_status, "Unexpected");

            var uiStatus = new UIStatusContentFormat();
            AddColumnsAndRow(uiStatus.UI_status, "Unexpected");

            var failPin = new FailPinLogContentFormat();
            AddColumnsAndRow(failPin.Fail_pin_rate_info, "Unexpected");

            Assert.False(recovery.CompareInfo(), nameof(recovery.CompareInfo));
            Assert.False(recovery.CompareRecoveryRate(), nameof(recovery.CompareRecoveryRate));
            Assert.False(rawData.CompareInfo(), nameof(rawData.CompareInfo));
            Assert.False(rawData.CompareStatistic(), nameof(rawData.CompareStatistic));
            Assert.False(testStatus.CompareInfo(), nameof(testStatus.CompareInfo));
            Assert.False(testStatus.CompareStatus(), nameof(testStatus.CompareStatus));
            Assert.False(uiStatus.CompareUiStatus(), nameof(uiStatus.CompareUiStatus));
            Assert.False(failPin.CompareInfo(), nameof(failPin.CompareInfo));
        }

        [Fact]
        public void CompareMethods_WhenRequiredColumnsAreMissingButRemainingColumnsAreKnown_ReturnCurrentBehaviorTrue()
        {
            var recovery = new RecoveryRateDataContentFormat();
            AddColumnsAndRow(recovery.LotInfo, CsvColumnNames.DbKeyWithSpace);
            AddColumnsAndRow(recovery.LotRecoveryRate, "Test_Item");

            var rawData = new RawDataContentFormat();
            AddColumnsAndRow(rawData.LotInfo, "Version");
            rawData.LotStatistic.Tables.Add(TableWithRow("Item No"));

            var testStatus = new TestStatusContentFormat();
            AddColumnsAndRow(testStatus.Tester_device_info, CsvColumnNames.DbKeyUnderscore);
            AddColumnsAndRow(testStatus.Tester_status, "DPW");

            var uiStatus = new UIStatusContentFormat();
            AddColumnsAndRow(uiStatus.UI_status, "Mac_Address");

            var failPin = new FailPinLogContentFormat();
            AddColumnsAndRow(failPin.Fail_pin_rate_info, "Mac Address");

            Assert.True(recovery.CompareInfo(), nameof(recovery.CompareInfo));
            Assert.True(recovery.CompareRecoveryRate(), nameof(recovery.CompareRecoveryRate));
            Assert.True(rawData.CompareInfo(), nameof(rawData.CompareInfo));
            Assert.True(rawData.CompareStatistic(), nameof(rawData.CompareStatistic));
            Assert.True(testStatus.CompareInfo(), nameof(testStatus.CompareInfo));
            Assert.True(testStatus.CompareStatus(), nameof(testStatus.CompareStatus));
            Assert.True(uiStatus.CompareUiStatus(), nameof(uiStatus.CompareUiStatus));
            Assert.True(failPin.CompareInfo(), nameof(failPin.CompareInfo));
        }

        [Fact]
        public void CompareMethods_WhenRequiredRowsOrTablesMissing_ReturnFalse()
        {
            Assert.False(new RecoveryRateDataContentFormat().CompareRecoveryRate());
            Assert.False(new RawDataContentFormat().CompareStatistic());
            Assert.False(new TestStatusContentFormat().CompareInfo());
            Assert.False(new TestStatusContentFormat().CompareStatus());
            Assert.False(new UIStatusContentFormat().CompareUiStatus());
        }

        private static DataTable TableWithRow(params string[] columns)
        {
            var table = new DataTable();
            AddColumnsAndRow(table, columns);
            return table;
        }

        private static void AddColumnsAndRow(DataTable table, params string[] columns)
        {
            foreach (string column in columns)
            {
                table.Columns.Add(column);
            }

            if (table.Rows.Count == 0)
            {
                table.Rows.Add(table.NewRow());
            }
        }
    }
}
