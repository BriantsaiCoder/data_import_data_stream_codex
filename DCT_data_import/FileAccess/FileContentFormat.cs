using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT_data_import
{
    //public class LotInfo
    //{
    //    public string Version { get; set; }
    //    public string Mac_Address { get; set; }
    //    public  string DB_Key { get; set; }
    //    public string Customer { get; set; }
    //    public string Package { get; set; }
    //    public string BondingDiagram { get; set; }
    //    public string Program { get; set; }
    //    public string Device { get; set; }
    //    public string Control_lot { get; set; }
    //    public string AO_lot { get; set; }
    //    public string OS_Machine_ID { get; set; }
    //    public string OS_Test_Board_ID { get; set; }
    //    public string User_ID { get; set; }
    //    public string Schedule_Lot { get; set; }
    //    public string File_Name { get; set; }
    //    public string Yield { get; set; }
    //    public string TOTAL { get; set; }
    //    public string PASS { get; set; }
    //    public string OPEN_PIN_FAIL { get; set; }
    //    public string SHORT_PIN_FAIL { get; set; }
    //    public string TOTAL_PPM { get; set; }
    //    public string OPEN_PIN_FAIL_PPM { get; set; }
    //    public string SHORT_PIN_FAIL_PPM { get; set; }
    //    public string Total_Test_Items { get; set; }
    //    public string Average_Test_Time { get; set; }
    //    public string Clear_Count { get; set; }
    //    public string Start { get; set; }
    //    public string Stop { get; set; }
    //    public List<string> OTHER_COLUMNS = new List<string>();
    //    public List<string> OTHER_COLUMNS_VALUE = new List<string>();

    //}
    public class RawDataContentFormat
    {
        private string[] infoColumns = { "Version", "Mac_Address", "DB_Key", "Customer", "Package", "BondingDiagram", "Program", "Device",
            "Control_lot", "AO_lot", "OS_Machine_ID", "OS_Test_Board_ID", "User_ID", "Schedule_Lot", "File_Name", "Yield(%)", "TOTAL", "PASS",
            "OPEN_PIN_FAIL", "SHORT_PIN_FAIL", "LEAKAGE_PIN_FAIL", "TOTAL_PPM", "OPEN_PIN_FAIL_PPM", "SHORT_PIN_FAIL_PPM",
            "LEAKAGE_PIN_FAIL_PPM", "Total_Test_Items", "Average_Test_Time", "Clear_Count", "Start", "Stop" };
        private string[] statisticColumns = { "Item No", "Item Name", "Force", "Wait time", "Spec MAX", "Spec MIN", "# of PASS", "# of FAIL", "MIN", "MAX", "AVG",
            "STDEV", "Cp", "Cpk", "Ppk", "unit", "value" };
        
        public string errMsg { get; set; }

        public DataTable lotInfo { get; set; }
        public DataSet lotStatistic { get; set; }
        public DataTable lotResult { get; set; }

        public RawDataContentFormat(string errMsg="")
        {
            this.errMsg = errMsg;
            lotInfo = new DataTable();
            // 因info只有一列資料，故先建立一個空DataRow
            DataRow dr = lotInfo.NewRow();
            lotInfo.Rows.Add(dr);

            lotStatistic = new DataSet();

            lotResult = new DataTable();
        }

        // 比對 infoColumns 與 lotInfo 的欄位
        public bool compareInfo()
        {
            if (this.lotInfo.Rows.Count < 1) return false;
            string[] columnNames = this.lotInfo.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();
            bool result = true; // infoColumns.SequenceEqual(columnNames);
            for (int i = 0; i < this.lotInfo.Columns.Count; i++)
            {
                if(!infoColumns.Contains(this.lotInfo.Columns[i].ColumnName))
                {
                    return false;
                }
            }

            return result;
        }

        // 比對 statisticColumns 與 lotStatistic 的欄位
        public bool compareStatistic()
        {
            if (this.lotStatistic.Tables.Count < 1) return false;
            string[] columnNames = this.lotStatistic.Tables[0].Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();
            bool result = true; // statisticColumns.SequenceEqual(columnNames);
            for (int i = 0; i < this.lotStatistic.Tables[0].Columns.Count; i++)
            {
                //Console.WriteLine(statisticColumns[i] +" vs " + this.lotStatistic.Tables[0].Columns[i]);
                if (!statisticColumns.Contains(this.lotStatistic.Tables[0].Columns[i].ColumnName))
                {
                    return false;
                }
            }

            return result;
        }

    }

    public class TestStatusContentFormat
    {
        private string[] infoColumns = { "DB_Key", "Mac_Address", "IP_Address", "Area", "Factory", "Machine Type", "Machine ID", "Customer",
            "Device Production", "Device Engineer", "Test Program", "Program_path", "Lot ID", "Wafer ID", "Execution mode", "Prober / Handler",
            "L/B ID", "Dut board type", "Efficiency check", "UI Flow checksum", "Yield", "File type", " Start Time", "End Time", "Lead_count", "Site_qty",
            "BD_Leak", "PG_Leak", "Wireclose_Leak" };
        private string[] statusColumns = { "DPW", "Duts", "CSV Name", "UPH", "Avg test time", "Max test time", "Min test time", "Avg index test time",
            "Max index test time", "Min index test time", "Diff time (die)", "End time (die)", "First time (die)", "Diff time (file)", "Conclusion file path",
            "Raw date file path", "S2S diff file path", "PASS / FAIL", "Case A Result", "Case B Result", "Case C Result", "PUI result", "PUI respond",
            "PUI file type", "PHI result", "PHI respond", "PHI file type", "TP result", "TP respond", "manual_data_module_csv_g result",
            "manual_data_module_csv_g respond", "data_module_stdf_g result", "data_module_stdf_g respond", "data_module_txt_g result",
            "data_module_txt_g respond", "data_module_std_g result", "data_module_std_g respond", "Test_Time_Module_csv_g result",
            "Test_Time_Module_csv_g respond", "data_module_act_smart1_txt result", "data_module_act_smart1_txt respond",
            "data_module_ASEKH_smart1_xml result", "data_module_ASEKH_smart1_xml respond", "data_module_act_fail_log result",
            "data_module_act_fail_log respond", "VIM result", "VIM respond", "VIM_open result", "VIM_open respond", "ViCbit result", "ViCbit respond",
            "ViCbit_open result", "ViCbit_open respond", "Pattern result", "Pattern respond" };
        
        public string errMsg { get; set; }
        public DataTable tester_device_info { get; set; }
        public DataTable tester_status { get; set; }
        public DataTable tester_sw_version { get; set; }
        public DataTable tester_production_analysis { get; set; }

        public TestStatusContentFormat(string errMsg = "")
        {
            this.errMsg = errMsg;
            tester_device_info = new DataTable();
            tester_status = new DataTable();
            tester_sw_version = new DataTable();
            tester_production_analysis = new DataTable();
        }

        // 比對 infoColumns 與 tester_device_info 的欄位
        public bool compareInfo()
        {
            if (this.tester_device_info.Rows.Count < 1) return false;
            string[] columnNames = this.tester_device_info.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();
            bool result = true; // infoColumns.SequenceEqual(columnNames);
            for (int i = 0; i < this.tester_device_info.Columns.Count; i++)
            {
                if (!infoColumns.Contains(this.tester_device_info.Columns[i].ColumnName))
                {
                    return false;
                }
            }

            return result;
        }

        // 比對 statusColumns 與 tester_status 的欄位
        public bool compareStatus()
        {
            if (this.tester_status.Rows.Count < 1) return false;
            string[] columnNames = this.tester_status.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();
            bool result = true; // infoColumns.SequenceEqual(columnNames);
            for (int i = 0; i < this.tester_status.Columns.Count; i++)
            {
                if (!statusColumns.Contains(this.tester_status.Columns[i].ColumnName))
                {
                    return false;
                }
            }

            return result;
        }

    }

    public class UIStatusContentFormat
    {
        private string[] uiStatusColumns = { "Mac_Address", "Area", "Factory", "OS_Machine", "Date", "Auto_learn", "DCT_Product_File_Setting_UI",
            "DCT_login_UI", "OS_self_diag_2K", "Pattonkan_UI", "DCT_I_V_Curve_Tool", "OS_TESTER_100mA_VI", "OS_TESTER_2A_VI", "OS_Tester_LCR_meter",
            "Wire_assignment_tool", "BGA_highlight_tool", "SimplificationUI", "OS_scan_tool", "DCT_UploadTp_UI", "DCT_AutoDownloadTp",
            "DCT_SW_Control_Tool", "DCT_DownloadTp_KH" };

        public DataTable UI_status { get; set; }

        public UIStatusContentFormat()
        {
            UI_status = new DataTable();
        }

        public bool compareUiStatus()
        {
            if (this.UI_status.Rows.Count < 1) return false;
            string[] columnNames = this.UI_status.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();
            bool result = true; // infoColumns.SequenceEqual(columnNames);
            for (int i = 0; i < this.UI_status.Columns.Count; i++)
            {
                if (!uiStatusColumns.Contains(this.UI_status.Columns[i].ColumnName))
                {
                    return false;
                }
            }

            return result;
        }

    }

    public class FailPinLogContentFormat
    {
        private string[] infoColumns = { "Mac Address", "DB Key", "Area", "Factory", "OS Machine", "AO Lot", "Mode", "Data format", "File Name", "Date" };

        public DataTable fail_pin_rate_info { get; set; }
        public DataTable fail_pin_rate_list { get; set; }
        public DataTable fail_pin_rate_list_pin_ball { get; set; }

        public FailPinLogContentFormat()
        {
            fail_pin_rate_info = new DataTable();
            DataRow dr = fail_pin_rate_info.NewRow();
            fail_pin_rate_info.Rows.Add(dr);

            fail_pin_rate_list = new DataTable();
            fail_pin_rate_list.Columns.Add("dut", typeof(string));
            fail_pin_rate_list.Columns.Add("site", typeof(string));
            fail_pin_rate_list.Columns.Add("fail_type", typeof(string));

            fail_pin_rate_list_pin_ball = new DataTable();
            fail_pin_rate_list_pin_ball.Columns.Add("fail_pin_rate_list_id", typeof(string));
            fail_pin_rate_list_pin_ball.Columns.Add("pin", typeof(string));
            fail_pin_rate_list_pin_ball.Columns.Add("ball", typeof(string));
            fail_pin_rate_list_pin_ball.Columns.Add("remark", typeof(string));
        }

        public bool compareInfo()
        {
            if (this.fail_pin_rate_info.Rows.Count < 1) return false;
            string[] columnNames = this.fail_pin_rate_info.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();
            bool result = true; // infoColumns.SequenceEqual(columnNames);
            for (int i = 0; i < this.fail_pin_rate_info.Columns.Count; i++)
            {
                if (!infoColumns.Contains(this.fail_pin_rate_info.Columns[i].ColumnName))
                {
                    return false;
                }
            }

            return result;
        }


    }

}
