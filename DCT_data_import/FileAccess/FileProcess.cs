using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static DCT_data_import.ApiObject;
namespace DCT_data_import
{
    public class FileProcess
    {
        //public WebApiClient webApiClient;
        private readonly WriteToLog writeToLog;
        public FileProcess()
        {
            //webApiClient = new WebApiClient();
            writeToLog = new WriteToLog();
        }
        public List<string> GetStringSegments(string original, int linesPerSegment = 0)
        {
            List<string> segments = new List<string>();
            int startIndex = 0;
            for (int i = 0; i < original.Length; i++)
            {
                //if (original[i] == '\n')
                //{
                //    newLinesEncountered++;
                //}
                if (original[i] == '\n'
                    || i == original.Length - 1)
                {
                    segments.Add(original.Substring(startIndex, (i - startIndex + 1)));
                    startIndex = i + 1;
                }
            }
            return segments;
        }
        #region file read
        public RawDataContentFormat FileReadRawData(StreamReader reader)
        {
            RawDataContentFormat fileContentFormat = new RawDataContentFormat();
            try
            {
                // 存放第二部分統計值的 Dictionary
                Dictionary<string, List<string>> statistic_dict = new Dictionary<string, List<string>>();
                // 存放第三部分  Serial, SN Num, SiteID, X, Y, HBIN, P/F
                List<List<string>> rawData_list = new List<List<string>>();
                // item 數量
                int item_count = 0;
                //FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                //StreamReader reader = new StreamReader(stream);
                int content_part = 1;
                // 第三部分  raw data的起始index
                int rawData_part_index = 0;
                string lines = reader.ReadToEnd();
                Console.WriteLine("text length: " + lines.Length);
                List<string> split_lines = GetStringSegments(lines);
                //List<string> split_lines = lines.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                for (int r = 0; r < split_lines.Count; r++)
                {
                    var line = split_lines[r].Trim();
                    if (!string.IsNullOrEmpty(line) && IsChinese(line))
                    {
                        fileContentFormat.errMsg = "Chinese word exists.";
                        //return new RawDataContentFormat("Chinese word exists.");
                    }
                    //}
                    //while (!reader.EndOfStream)
                    //{
                    //    var line = reader.ReadLine();
                    //Console.WriteLine(line);
                    //Console.Write(r.ToString() + ",");
                    //if (r == 51454)
                    //{
                    //    Console.WriteLine("");
                    //}
                    var values = line.Split(',', '\0', '\r', '\n');
                    var values_tmp = values;
                    // 去除空白值
                    if (content_part != 3)
                    {
                        values_tmp = values.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                    }
                    if (values_tmp.Length < 1) continue;
                    //Console.WriteLine("values : " + values.Length);
                    //Console.WriteLine(values[0]);
                    // 看到關鍵字 "Serial"
                    if (values[0] == "Serial")
                    {
                        content_part = 3;
                    }
                    // 第一部分  info
                    if (content_part == 1)
                    {
                        fileContentFormat.lotInfo.Columns.Add(values[0].Split(':')[0], typeof(string));
                        fileContentFormat.lotInfo.Rows[0][values[0].Split(':')[0]] = values[1];
                        //fileContentFormat.setLotInfo(lotInfo, values[0], values[1]);
                    }
                    // 第二部分  statistic
                    else if (content_part == 2)
                    {
                        // 找到第一個非空值的index
                        for (int i = 0; i < values.Length; i++)
                        {
                            if (!string.IsNullOrEmpty(values[i]))
                            {
                                // 將第一個非空值之前的所有空值砍掉
                                values = values.Where(s => values.ToList().IndexOf(s) >= i).ToArray();
                                // 前有7格空白表示在統計值區塊的 Item_NO 或 Item_name
                                if (i == 7)
                                {
                                    if (values[0] == "1001")
                                    {
                                        statistic_dict.Add("Item No", values.ToList<string>());
                                        item_count = values.Length;
                                    }
                                    else
                                    {
                                        // 只取到item數量為止，在後方的則是註記欄位，如 test time、index time
                                        values = values.Where(s => values.ToList().IndexOf(s) < item_count).ToArray();
                                        statistic_dict.Add("Item Name", values.ToList<string>());
                                    }
                                }
                                else
                                {
                                    // 屬於欄位: Force, Wait time, Spec MAX, Spec MIN, # of PASS, # of FAIL, MIN, MAX, AVG, STDEV, Cp, Cpk
                                    var values_except_head = values.Where(x => x != values[0]).ToArray();
                                    values_except_head = values_except_head.Where(s => values_except_head.ToList().IndexOf(s) < item_count).ToArray();
                                    statistic_dict.Add(values[0], values_except_head.ToList<string>());
                                }
                                break;
                            }
                        }
                    }
                    // 第三部分  raw data
                    else if (content_part == 3)
                    {
                        // unit 值填入 Dictionary
                        if (values[0] == "Serial")
                        {
                            var values_unit = values.Where(s => values.ToList().IndexOf(s) >= 7).ToArray();
                            values_unit = values_unit.Where(s => values_unit.ToList().IndexOf(s) < item_count).ToArray();
                            statistic_dict.Add("unit", values_unit.ToList<string>());
                        }
                        int result_part = 1;  // 1.表示Serial,SN Num,SiteID,	X,Y,HBIN,P/F   2.表示 raw data值的部分含單位(V, uA,...)
                        DataRow dr_lotResult = fileContentFormat.lotResult.NewRow();
                        for (int i = 0; i < values.Length; i++)
                        {
                            //if (r == 51454)
                            //{
                            //    Console.Write(i.ToString() + ",");
                            //    if (i == 639)
                            //    {
                            //        Console.WriteLine("");
                            //    }
                            //}
                            // 欄位 Serial, SN Num, SiteID,	 X, Y, HBIN, P/F
                            if (result_part == 1 && values[0] == "Serial")
                            {
                                fileContentFormat.lotResult.Columns.Add(values[i], typeof(string));
                            }
                            // 欄位 Serial 為空的直接跳過
                            else if (string.IsNullOrEmpty(values[0].Trim()))
                            {
                                continue;
                            }
                            // 欄位 Serial, SN Num, SiteID,	 X, Y, HBIN, P/F 的 values
                            else if (result_part == 1)
                            {
                                dr_lotResult[i] = values[i];
                            }
                            // unit
                            if (result_part == 2 && values[0] == "Serial")
                            {
                                // 第三部分最右方 test time, index time, real time
                                if (i == rawData_part_index + item_count)
                                {
                                    fileContentFormat.lotResult.Columns.Add("test time", typeof(string));
                                    fileContentFormat.lotResult.Columns.Add("index time", typeof(string));
                                    fileContentFormat.lotResult.Columns.Add("real time", typeof(string));
                                    break;
                                }
                                rawData_list.Add(new List<string>());
                            }
                            // raw data value的部分 先存入rawData_list
                            else if (result_part == 2 && i < rawData_part_index + rawData_list.Count)
                            {
                                // 讀值若含有小數點"."而沒有小數位，則移除小數點
                                if (values[i].Substring(values[i].Length - 1) == ".") values[i] = values[i].Substring(0, values[i].Length - 1);
                                rawData_list[i - rawData_part_index].Add(values[i]);
                            }
                            else if (result_part == 2 && i >= rawData_part_index + rawData_list.Count)
                            {
                                if (i - rawData_list.Count >= fileContentFormat.lotResult.Columns.Count)
                                {
                                    //break;
                                    throw new ArgumentException("Read value column count greater than expected.");
                                }
                                dr_lotResult[i - rawData_list.Count] = values[i];
                            }
                            // 以"P/F"為分界
                            if (values[i] == "P/F" || i == rawData_part_index - 1)
                            {
                                result_part = 2;
                                rawData_part_index = i + 1;
                            }
                        }
                        if (values[0] != "Serial")
                        {
                            fileContentFormat.lotResult.Rows.Add(dr_lotResult);
                        }
                    }
                    // 看到關鍵字 "Stop"
                    if (values[0] == "Stop:")
                    {
                        content_part = 2;
                    }
                }
                // 釋放記憶體
                split_lines = null;
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
                // 將raw data values 填入統計值的表
                DataTable item_table = new DataTable();
                for (int i = 0; i < statistic_dict.Keys.Count; i++)
                {
                    item_table.Columns.Add(statistic_dict.ElementAt(i).Key, typeof(string));
                }
                item_table.Columns.Add("value", typeof(string));
                for (int i = 0; i < item_count; i++)
                {
                    //Console.Write(i.ToString() + ",");
                    //if (i == 330)
                    //{
                    //    var memSz = GC.GetTotalMemory(false) / 1024 / 1024;
                    //    Console.WriteLine($"Managed Heap = {memSz}MB");
                    //}
                    // Clone 方法建立的新 DataTable 不會包含任何 DataRows.，只有相同的Schema。
                    DataTable item_table_tmp = item_table.Clone();
                    DataRow dataRow = item_table_tmp.NewRow();
                    // 對 dataRow 填入 統計值
                    for (int j = 0; j < statistic_dict.Keys.Count; j++)
                    {
                        dataRow[statistic_dict.ElementAt(j).Key] = statistic_dict[statistic_dict.ElementAt(j).Key][i];
                    }
                    // 對 dataRow 填入 raw data list
                    if (rawData_list.Count > 0)
                    {
                        string strJoin = String.Join(", ", rawData_list[i].ToArray());
                        if (string.IsNullOrEmpty(strJoin.Trim()))
                        {
                            dataRow["value"] = "[]";
                        }
                        else
                        {
                            dataRow["value"] = "[" + String.Join(", ", rawData_list[i].ToArray()) + "]";
                        }
                        //dataRow["value"] = "[";
                        //for (int k = 0; k < rawData_list[i].Count; k++)
                        //{
                        //    if (string.IsNullOrEmpty(rawData_list[i][k]))
                        //    {
                        //        dataRow["value"] += "null";
                        //    }
                        //    else
                        //    {
                        //        dataRow["value"] += rawData_list[i][k];
                        //    }
                        //    if(k!= rawData_list[i].Count-1)
                        //    {
                        //        dataRow["value"] += ",";
                        //    }
                        //    else
                        //    {
                        //        dataRow["value"] += "]";
                        //    }
                        //}
                    }
                    else
                    {
                        dataRow["value"] = "[]";
                    }
                    if (dataRow["value"].ToString().Substring(dataRow["value"].ToString().Length - 1) != "]")
                    {
                        dataRow["value"] += "]";
                    }
                    item_table_tmp.Rows.Add(dataRow);
                    fileContentFormat.lotStatistic.Tables.Add(item_table_tmp);
                }
                //stream.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                fileContentFormat.errMsg = "讀檔內容錯誤";
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
                return fileContentFormat;
            }
            return fileContentFormat;
        }
        public TestStatusContentFormat FileReadTesterStatus(StreamReader reader)
        {
            TestStatusContentFormat testStatusContentFormat = new TestStatusContentFormat();
            try
            {
                int content_part = 1;
                //using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                //{
                //    using (var reader = new StreamReader(stream))
                //    {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    //Console.WriteLine(line);
                    var values = eraseSpecificChar(line);
                    if (values.Length < 1) continue;
                    //Console.WriteLine("values : " + values_tmp.Length);
                    if (values[0] == "Device information")
                    {
                        content_part = 1;
                        continue;
                    }
                    else if (values[0] == "Tester status")
                    {
                        content_part = 2;
                        continue;
                    }
                    else if (values[0] == "SW version")
                    {
                        content_part = 3;
                        continue;
                    }
                    else if (values[0] == "Production analysis")
                    {
                        content_part = 4;
                        continue;
                    }
                    switch (content_part)
                    {
                        case 1:
                            if (values[0] == "DB_Key")
                            {
                                for (int i = 0; i < values.Length; i++)
                                {
                                    testStatusContentFormat.tester_device_info.Columns.Add(values[i], typeof(string));
                                }
                            }
                            else if (testStatusContentFormat.tester_device_info.Columns.Count < 1)
                            {
                                return null;
                            }
                            else
                            {
                                DataRow dr_tester_device_info = testStatusContentFormat.tester_device_info.NewRow();
                                for (int i = 0; i < testStatusContentFormat.tester_device_info.Columns.Count; i++)
                                {
                                    dr_tester_device_info[i] = values[i];
                                }
                                testStatusContentFormat.tester_device_info.Rows.Add(dr_tester_device_info);
                            }
                            break;
                        case 2:
                            if (values[0] == "DPW")
                            {
                                for (int i = 0; i < values.Length; i++)
                                {
                                    testStatusContentFormat.tester_status.Columns.Add(values[i], typeof(string));
                                }
                            }
                            else
                            {
                                DataRow dr_tester_status = testStatusContentFormat.tester_status.NewRow();
                                for (int i = 0; i < values.Length; i++)
                                {
                                    dr_tester_status[i] = values[i];
                                }
                                testStatusContentFormat.tester_status.Rows.Add(dr_tester_status);
                            }
                            break;
                        case 3:
                            if (values[0] == "PUI version")
                            {
                                for (int i = 0; i < values.Length; i++)
                                {
                                    testStatusContentFormat.tester_sw_version.Columns.Add(values[i], typeof(string));
                                }
                            }
                            else
                            {
                                DataRow dr_tester_sw_version = testStatusContentFormat.tester_sw_version.NewRow();
                                for (int i = 0; i < values.Length; i++)
                                {
                                    dr_tester_sw_version[i] = values[i];
                                }
                                testStatusContentFormat.tester_sw_version.Rows.Add(dr_tester_sw_version);
                            }
                            break;
                        case 4:
                            if (values[0] == "site1_yield")
                            {
                                for (int i = 0; i < values.Length; i++)
                                {
                                    testStatusContentFormat.tester_production_analysis.Columns.Add(values[i], typeof(string));
                                }
                            }
                            else
                            {
                                DataRow dr_tester_production_analysis = testStatusContentFormat.tester_production_analysis.NewRow();
                                for (int i = 0; i < values.Length; i++)
                                {
                                    dr_tester_production_analysis[i] = values[i];
                                }
                                testStatusContentFormat.tester_production_analysis.Rows.Add(dr_tester_production_analysis);
                                return testStatusContentFormat;
                            }
                            break;
                        default:
                            break;
                    }
                    //Console.WriteLine(line);
                }
                //    }
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
            return testStatusContentFormat;
        }
        public UIStatusContentFormat FileReadUIStatus(StreamReader reader)
        {
            UIStatusContentFormat uiStatusContentFormat = new UIStatusContentFormat();
            try
            {
                //using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                //{
                //    using (var reader = new StreamReader(stream))
                //    {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    //Console.WriteLine(line);
                    var values = eraseSpecificChar(line);
                    if (values.Length < 1) continue;
                    //Console.WriteLine("values : " + values_tmp.Length);
                    if (values[0] == "Mac_Address")
                    {
                        for (int i = 0; i < values.Length; i++)
                        {
                            uiStatusContentFormat.UI_status.Columns.Add(values[i], typeof(string));
                        }
                    }
                    else
                    {
                        DataRow dr_UI_status = uiStatusContentFormat.UI_status.NewRow();
                        for (int i = 0; i < values.Length; i++)
                        {
                            dr_UI_status[i] = values[i];
                        }
                        uiStatusContentFormat.UI_status.Rows.Add(dr_UI_status);
                    }
                }
                //    }
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
            return uiStatusContentFormat;
        }
        public FailPinLogContentFormat FileReadFailPinLog(StreamReader reader)
        {
            FailPinLogContentFormat failPinLogContentFormat = new FailPinLogContentFormat();
            try
            {
                string data_format = "";
                //using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                //{
                //    using (var reader = new StreamReader(stream))
                //    {
                int content_part = 1;
                int fail_pin_list_id = 0;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = eraseSpecificChar(line);
                    if (values == null)
                    {
                        continue;
                    }
                    if (values.Length < 1) continue;
                    // 看到關鍵字 "Data format" ，取得pin/ball類型
                    if (values[0] == "Data format")
                    {
                        data_format = values[1];
                    }
                    // 看到關鍵字 "DUT"
                    if (values[0] == "DUT")
                    {
                        content_part = 2;
                        continue;
                    }
                    // fail pin rate的上半部分
                    if (content_part == 1)
                    {
                        failPinLogContentFormat.fail_pin_rate_info.Columns.Add(values[0], typeof(string));
                        failPinLogContentFormat.fail_pin_rate_info.Rows[0][values[0]] = (values.Length > 1) ? values[1] : "";
                    }
                    // fail pin rate的下半部分
                    else if (content_part == 2)
                    {
                        if (values.Length >= 3)
                        {
                            DataRow dr_fail_pin_rate_list = failPinLogContentFormat.fail_pin_rate_list.NewRow();
                            // 欄位 DUT, Site, Fail Type
                            for (int i = 0; i < 3; i++)
                            {
                                dr_fail_pin_rate_list[i] = values[i];
                            }
                            failPinLogContentFormat.fail_pin_rate_list.Rows.Add(dr_fail_pin_rate_list);
                            // 讀取 fail pin 與 log 存到 List
                            int fail_pin_part = 1;
                            List<string> fail_pin_list = new List<string>();
                            List<string> fail_pin_log = new List<string>();
                            for (int i = 3; i < values.Length; i++)
                            {
                                // 以 ';' 分開fail pin log 與 remark
                                if (values[i] == ";")
                                {
                                    fail_pin_part = 2;
                                    continue;
                                }
                                if (fail_pin_part == 1)
                                {
                                    fail_pin_list.Add(values[i]);
                                }
                                else if (fail_pin_part == 2)
                                {
                                    fail_pin_log.Add(values[i]);
                                }
                            }
                            fail_pin_list_id++;
                            //  將 fail pin 與 log 存到 DataTable
                            for (int i = 0; i < fail_pin_list.Count; i++)
                            {
                                DataRow dr_fail_pin_rate_list_pin_ball = failPinLogContentFormat.fail_pin_rate_list_pin_ball.NewRow();
                                string[] value_split = fail_pin_list[i].Split('(', ')');
                                if (data_format == "Pin")
                                {
                                    dr_fail_pin_rate_list_pin_ball["pin"] = value_split[0];
                                    dr_fail_pin_rate_list_pin_ball["ball"] = value_split[1];
                                }
                                else if (data_format == "Ball")
                                {
                                    dr_fail_pin_rate_list_pin_ball["ball"] = value_split[0];
                                    dr_fail_pin_rate_list_pin_ball["pin"] = value_split[1];
                                }
                                dr_fail_pin_rate_list_pin_ball["fail_pin_rate_list_id"] = fail_pin_list_id;
                                dr_fail_pin_rate_list_pin_ball["remark"] = String.Join(",", fail_pin_log.ToArray());
                                failPinLogContentFormat.fail_pin_rate_list_pin_ball.Rows.Add(dr_fail_pin_rate_list_pin_ball);
                            }
                        }
                    }
                }
                //    }
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
            return failPinLogContentFormat;
        }
        #endregion file read
        public bool isFileNameExistInDB(string fileName, WebApiClient webApiClient)
        {
            var pool_excute = new Pool_excute
            {
                pool = Program.POOL_NAME,
                query = "SELECT file_name FROM lots_info where file_name='" + fileName + "';"
            };
            Pool_excute_response response = webApiClient.ExcutePoolAsync(pool_excute).GetAwaiter().GetResult();
            //dynamic json_str = JObject.Parse(response.data);
            //JArray items = (JArray)json_str["data"];
            int length = 0;
            if (response.data != null)
            {
                length = response.data.Count;
            }
            //Console.WriteLine(json_str.data);
            return (length > 0);
        }
        public bool isDBKeyExistInDB(string db_table_name, string db_key, WebApiClient webApiClient)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var pool_excute = new Pool_excute
            {
                pool = Program.POOL_NAME,
                query = "SELECT db_key FROM " + db_table_name + " where db_key='" + db_key + "';"
            };
            Pool_excute_response response = webApiClient.ExcutePoolAsync(pool_excute).GetAwaiter().GetResult();
            //dynamic json_str = JObject.Parse(response.data);
            //JArray items = (JArray)json_str["data"];
            if (!string.IsNullOrEmpty(response.error))
            {
                string result = PoolException(new Exception(response.error), webApiClient);
                return false;
            }
            int length = 0;
            if (response.data != null)
            {
                length = response.data.Count;
            }
            stopwatch.Stop();
            long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            //Console.WriteLine(json_str.data);
            return (length > 0);
        }
        public bool isUIStatusDataExistInDB(string mac_address, string area, string factory, string os_machine, string date, WebApiClient webApiClient)
        {
            //string whereDate = (string.IsNullOrEmpty(date) || date == "null") ? "" : "' AND date='" + date;
            if (string.IsNullOrEmpty(date) || date == "null")
            {
                return false;
            }
            var pool_excute = new Pool_excute
            {
                pool = Program.POOL_NAME,
                query = "SELECT id FROM ui_status WHERE mac_address='" + mac_address + "' AND area='" + area + "' AND factory='" + factory + "' AND os_machine='" + os_machine + "' AND date='" + date + "' ; "
            };
            Pool_excute_response response = webApiClient.ExcutePoolAsync(pool_excute).GetAwaiter().GetResult();
            //dynamic json_str = JObject.Parse(response.data);
            //JArray items = (JArray)json_str["data"];
            int length = 0;
            if (response.data != null)
            {
                length = response.data.Count;
            }
            //Console.WriteLine(json_str.data);
            return (length > 0);
        }
        public string[] eraseSpecificChar(string str_line)
        {
            string[] values = str_line.Split(',', '\0', '\r', '\n');
            // 去除空白值
            string[] values_tmp1 = values.Where(x => !string.IsNullOrEmpty(x)).ToArray();
            int first_value_idx = -1, last_value_idx = -1;
            //去除頭尾空白
            for (int i = 0; i < values.Length; i++)
            {
                if (first_value_idx == -1 && !string.IsNullOrEmpty(values[i]))
                {
                    first_value_idx = i;
                    last_value_idx = i;
                }
                else if (!string.IsNullOrEmpty(values[i]))
                {
                    last_value_idx = i;
                }
            }
            if (first_value_idx == -1) return null;
            string[] values_tmp = new string[0] { };
            Array.Resize(ref values_tmp, last_value_idx - first_value_idx + 1);
            for (int i = 0; i <= last_value_idx - first_value_idx; i++)
            {
                values_tmp[i] = values[first_value_idx + i];
            }
            return values_tmp;
        }
        // 解析日期格式 "Jun_06_2022_12_08_22"
        public string customizeDateTimeParser(string datetime)
        {
            string[] time_split = datetime.Split('_');
            if (time_split.Length != 6) return "";
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
        // RawData 匯入資料庫
        public bool importRawData(RawDataContentFormat content, WebApiClient webApiClient)
        {
            if (content.lotInfo.Rows.Count < 1 || content.lotStatistic.Tables.Count < 1) return false;
            // assign 需要 insert 的 欄位名稱 與 values
            string columns = "", values = "";
            Pool_excute_response response2;
            #region insert raw data 的 info 表格
            for (int i = 0; i < content.lotInfo.Columns.Count; i++)
            {
                string column_name = content.lotInfo.Columns[i].ColumnName.ToLower();
                column_name = column_name.Split('(', ')')[0];
                // 欄位名稱調整
                if (column_name == "bondingdiagram") column_name = "bonding_diagram";
                if (column_name == "Pass without OCR".ToLower()) column_name = "pass_without_ocr";
                if (column_name == "OPEN without OCR".ToLower()) column_name = "open_without_ocr";
                if (column_name == "Short & Others".ToLower()) column_name = "short_others";
                if (column_name == "Pass without OCR_PPM".ToLower()) column_name = "pass_without_ocr_ppm";
                if (column_name == "OPEN without OCR_PPM".ToLower()) column_name = "open_without_ocr_ppm";
                if (column_name == "Short & Others_PPM".ToLower()) column_name = "short_others_ppm";
                // 處理日期格式
                if (column_name == "start" || column_name == "stop")
                {
                    // 日期格式解析正確
                    content.lotInfo.Rows[0][i] = customizeDateTimeParser(content.lotInfo.Rows[0][i].ToString());
                }
                columns += "`" + column_name.Trim() + "`";
                values += "\"" + ConvertEmptyToDefaultString(content.lotInfo.Rows[0][i].ToString().Trim()) + "\"";
                //values += "\"" + content.lotInfo.Rows[0][i].ToString().Trim() + "\"";
                if (i != content.lotInfo.Columns.Count - 1)
                {
                    columns += ",";
                    values += ",";
                }
            }
            try
            {
                response2 = executeInsertWithAPI(webApiClient, "lots_info", columns, values);
                //if (!string.IsNullOrEmpty(response2.error))
                //{
                //    if(response2.error.Contains("Please initiate connection pool first using the init function"))
                //    {
                //        writeToLog.writeToLog("'INSERT INTO lots_info' error:" + response2.error);
                //        return false;
                //    }
                //    return false;
                //}
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                writeToLog.writeToLog("'INSERT INTO lots_info' error:" + ex.ToString());
                return false;
            }
            #endregion
            string lotId = "";
            try
            {
                // 取得當前 lot id 值
                lotId = response2.data[0]["insertId"].ToString();
            }
            catch (Exception ex)
            {
                writeToLog.writeToLog("'取得當前 lot id 值 error:" + ex.ToString());
                Console.WriteLine(ex.ToString());
                return false;
            }
            //string lotId = "3";
            #region insert raw data 的 statistic 表格
            columns = "`lot_id`,"; values = "";
            for (int i = 0; i < content.lotStatistic.Tables[0].Columns.Count; i++)
            {
                string column_name = content.lotStatistic.Tables[0].Columns[i].ColumnName.ToLower();
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
                if (i != content.lotStatistic.Tables[0].Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert 統計值表
            int test_count = 0, cut_size = 0;
            int.TryParse(content.lotStatistic.Tables[0].Rows[0]["# of PASS"].ToString(), out test_count);
            int tableCount = content.lotStatistic.Tables.Count;
            //if (test_count* tableCount < 30000) cut_size = 50;
            //else if (test_count * tableCount < 60000) cut_size = 10;
            //else if (test_count * tableCount < 100000) cut_size =2;
            //else  cut_size = 1;
            cut_size = (test_count > 0 && test_count < 10000) ? 10000 / test_count : 1;
            int lotResultCount = content.lotResult.Rows.Count;
            Console.WriteLine("itemCount=" + tableCount + " lotResultCount= " + lotResultCount);
            //cut_size = 1;
            //Console.WriteLine("test_count: " + test_count + "    cut_size: " + cut_size);
            try
            {
                values = "";
                for (int i = 0; i < content.lotStatistic.Tables.Count; i++)
                {
                    if (content.lotStatistic.Tables[i].Rows.Count < 1) continue;
                    values += "(\"" + ConvertEmptyToDefaultString(lotId) + "\",";
                    //values += "(\"" + lotId + "\",";
                    values += "\"" + string.Join("\",\"", content.lotStatistic.Tables[i].Rows[0].ItemArray.Select(item => ConvertEmptyToDefaultString(item?.ToString()))) + "\"";
                    //values += "\"" + string.Join("\",\"", content.lotStatistic.Tables[i].Rows[0].ItemArray) + "\"";
                    //for (int j = 0; j < content.lotStatistic.Tables[i].Columns.Count; j++)
                    //{
                    //    values += "\"" + content.lotStatistic.Tables[i].Rows[0][j].ToString().Trim() + "\"";
                    //    if (j != content.lotStatistic.Tables[i].Columns.Count - 1)
                    //    {
                    //        values += ",";
                    //    }
                    //}
                    // 每cut_size個row就匯入一次
                    if (i != 0 && i % cut_size == 0)
                    {
                        values += ")";
                        values = values.Substring(1, values.Length - 2);
                        response2 = executeInsertWithAPI(webApiClient, "lots_statistic", columns, values);
                        if (Program.HOST == "192.168.0.105") response2 = executeInsertWithAPI(webApiClient, "lots_statistic_str", columns, values); // 只給舊win server使用
                        //Thread.Sleep(500);
                        if (!string.IsNullOrEmpty(response2.error))
                        {
                            writeToLog.writeToLog("'INSERT INTO lots_statistic' response error:" + response2.error);
                            response2 = deleteRawData(webApiClient, lotId);
                            return false;
                        }
                        values = "";
                    }
                    else if (i != content.lotStatistic.Tables.Count - 1)
                    {
                        values += "),";
                    }
                    else
                    {
                        values += ")";
                    }
                    //Thread.Sleep(50);
                }
                if (values.Length > 3)
                {
                    values = values.Substring(1, values.Length - 2);
                    response2 = executeInsertWithAPI(webApiClient, "lots_statistic", columns, values);
                    if (Program.HOST == "192.168.0.105") response2 = executeInsertWithAPI(webApiClient, "lots_statistic_str", columns, values); // 只給舊win server使用
                    if (!string.IsNullOrEmpty(response2.error))
                    {
                        writeToLog.writeToLog("'INSERT INTO lots_statistic' response error:" + response2.error);
                        response2 = deleteRawData(webApiClient, lotId);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                string poolExceptionResult = PoolException(ex, webApiClient);
                //Console.WriteLine(ex.ToString());
                writeToLog.writeToLog("'INSERT INTO lots_statistic' error:" + ex.ToString());
                response2 = deleteRawData(webApiClient, lotId);
                return false;
            }
            #endregion
            #region insert raw data 的 result 表格
            cut_size = (content.lotResult.Rows.Count > 5000) ? 5000 : content.lotResult.Rows.Count;
            columns = "`lot_id`,"; values = "";
            for (int i = 0; i < content.lotResult.Columns.Count; i++)
            {
                string column_name = content.lotResult.Columns[i].ColumnName.ToLower();
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
                if (i != content.lotResult.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert result表
            try
            {
                values = "";
                for (int i = 0; i < content.lotResult.Rows.Count; i++)
                {
                    //// 判斷 index=1 的Serial是否為空，若為空則跳過
                    //if (string.IsNullOrEmpty(content.lotResult.Rows[0][0].ToString())) continue;
                    if (!string.IsNullOrEmpty(content.lotResult.Rows[i]["Serial"].ToString()))
                    {
                        values += "(\"" + ConvertEmptyToDefaultString(lotId) + "\",";
                        //values += "(\"" + lotId + "\",";
                        for (int j = 0; j < content.lotResult.Columns.Count; j++)
                        {
                            if ((content.lotResult.Columns[j].ColumnName == "SN Num" || content.lotResult.Columns[j].ColumnName == "SiteID" || content.lotResult.Columns[j].ColumnName == "real time" || content.lotResult.Columns[j].ColumnName == "X" || content.lotResult.Columns[j].ColumnName == "Y" || content.lotResult.Columns[j].ColumnName == "P/F") && content.lotResult.Rows[i][j].ToString().Trim() == "")
                            {
                                values += "NULL";
                            }
                            else if ((content.lotResult.Columns[j].ColumnName == "test time" || content.lotResult.Columns[j].ColumnName == "index time") && content.lotResult.Rows[i][j].ToString().Trim() == "")
                            {
                                values += "0";
                            }
                            else if (content.lotResult.Columns[j].ColumnName == "real time")
                            {
                                // 判斷是否為時間格式，若不符合則給NULL
                                DateTime out_dateTime;
                                if (DateTime.TryParse(content.lotResult.Rows[i][j].ToString().Trim(), out out_dateTime))
                                {
                                    values += "\"" + ConvertEmptyToDefaultString(content.lotResult.Rows[i][j].ToString()) + "\"";
                                    //values += "\"" + content.lotResult.Rows[i][j].ToString().Trim() + "\"";
                                }
                                else
                                {
                                    values += "NULL";
                                }
                            }
                            else
                            {
                                values += "\"" + ConvertEmptyToDefaultString(content.lotResult.Rows[i][j].ToString()) + "\"";
                                //values += "\"" + content.lotResult.Rows[i][j].ToString().Trim() + "\"";
                            }
                            if (j != content.lotResult.Columns.Count - 1)
                            {
                                values += ",";
                            }
                        }
                        // 每cut_size個row就匯入一次
                        if (i != 0 && i % cut_size == 0 && !string.IsNullOrEmpty(values))
                        {
                            values += ")";
                            values = values.Substring(1, values.Length - 2);
                            response2 = executeInsertWithAPI(webApiClient, "lots_result", columns, values);
                            if (!string.IsNullOrEmpty(response2.error))
                            {
                                writeToLog.writeToLog("'INSERT INTO lots_result' error:" + response2.error);
                                //response2 = deleteRawData(webApiClient, lotId);
                                return false;
                            }
                            values = "";
                        }
                        else if (i != content.lotResult.Rows.Count - 1)
                        {
                            values += "),";
                        }
                        else
                        {
                            values += ")";
                        }
                    }
                }
                if (!string.IsNullOrEmpty(values))
                {
                    values = values.Substring(1, values.Length - 2);
                    // 如果最後一個字元是')' 則移除
                    string last_str = values.Substring(values.Length - 1);
                    if (last_str == ")")
                    {
                        values = values.Substring(0, values.Length - 1);
                    }
                    response2 = executeInsertWithAPI(webApiClient, "lots_result", columns, values);
                    if (!string.IsNullOrEmpty(response2.error))
                    {
                        writeToLog.writeToLog("'INSERT INTO lots_result' response error:" + response2.error);
                        //response2 = deleteRawData(webApiClient, lotId);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                string poolExceptionResult = PoolException(ex, webApiClient);
                //Console.WriteLine(ex.ToString());
                writeToLog.writeToLog("'INSERT INTO lots_result' error:" + ex.ToString());
                //response2 = deleteRawData(webApiClient, lotId);
                return false;
            }
            #endregion
            return true;
        }
        // Tester Status 匯入資料庫
        public bool importTesterStatus(TestStatusContentFormat content, WebApiClient webApiClient)
        {
            if (content.tester_device_info.Rows.Count < 1 || content.tester_status.Rows.Count < 1 || content.tester_sw_version.Rows.Count < 1) return false;
            // assign 需要 insert 的 欄位名稱 與 values
            string columns = "", values = "";
            Pool_excute_response response;
            #region insert `tester_device_info`
            for (int i = 0; i < content.tester_device_info.Columns.Count; i++)
            {
                string column_name = content.tester_device_info.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                // 欄位名稱調整
                if (column_name == "prober_/_handler") column_name = "prober/handler";
                if (column_name == "l/b_id") column_name = "L/B_id";
                if (column_name == "handler_repair_starttime") column_name = "handler_repair_start_time";
                if (column_name == "handler_repair_endtime") column_name = "handler_repair_end_time";
                string[] numTypeColumn = { "efficiency_check", "ui_flow_checksum", "yield", "lead_count", "site_qty", "bd_leak", "pg_leak", "wireclose_leak" };
                if (numTypeColumn.Contains(column_name) && (content.tester_device_info.Rows[0][i].ToString().Trim() == "" || content.tester_device_info.Rows[0][i].ToString().Trim() == "NA"))
                {
                    values += "NULL";
                }
                else
                {
                    // 路徑的(\) 要處理成 (\\)
                    if (column_name == "program_path")
                    {
                        values += "\"" + ConvertEmptyToDefaultString(content.tester_device_info.Rows[0][i].ToString()).Replace(@"\", @"\\") + "\"";
                        //values += "\"" + content.tester_device_info.Rows[0][i].ToString().Trim().Replace(@"\", @"\\") + "\"";
                    }
                    else if (column_name == "start_time" || column_name == "end_time")
                    {
                        DateTime datetime = new DateTime();
                        if (DateTime.TryParse(content.tester_device_info.Rows[0][i].ToString().Trim(), out datetime))
                        {
                            values += "\"" + datetime.ToString("yyyy-MM-dd HH:mm:ss") + "\"";
                        }
                        else
                        {
                            values += "null";
                        }
                    }
                    else
                    {
                        values += "\"" + ConvertEmptyToDefaultString(content.tester_device_info.Rows[0][i].ToString()) + "\"";
                        //values += "\"" + content.tester_device_info.Rows[0][i].ToString().Trim() + "\"";
                    }
                }
                columns += "`" + column_name.Trim() + "`";
                //values += "\"" + content.tester_device_info.Rows[0][i].ToString().Trim() + "\"";
                if (i != content.tester_device_info.Columns.Count - 1)
                {
                    columns += ",";
                    values += ",";
                }
            }
            try
            {
                response = executeInsertWithAPI(webApiClient, "tester_device_info", columns, values);
                if (!string.IsNullOrEmpty(response.error))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                string poolExceptionResult = PoolException(ex, webApiClient);
                //Console.WriteLine(ex.ToString());
                writeToLog.writeToLog("'INSERT INTO tester_device_info' error:" + ex.ToString());
                return false;
            }
            #endregion
            string device_info_Id = "";
            try
            {
                //string db_key = content.tester_device_info.Rows[0]["DB_Key"].ToString();
                //pool_excute = new Pool_excute
                //{
                //    pool = Program.POOL_NAME,
                //    query = "SELECT id FROM `tester_device_info` WHERE `db_key` = '" + db_key + "';"
                //};
                //response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
                device_info_Id = response.data[0]["insertId"].ToString();
            }
            catch (Exception ex)
            {
                writeToLog.writeToLog("get tester_device_info insertId error:" + ex.ToString());
                return false;
            }
            #region insert `tester_status`
            columns = "`device_info_Id`,"; values = "";
            for (int i = 0; i < content.tester_status.Columns.Count; i++)
            {
                string column_name = content.tester_status.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                if (column_name == "diff_time_(die)") column_name = "diff_time_die";
                if (column_name == "end_time_(die)") column_name = "end_time_die";
                if (column_name == "first_time_(die)") column_name = "first_time_die";
                if (column_name == "diff_time_(file)") column_name = "diff_time_file";
                if (column_name == "pass_/_fail") column_name = "pass/fail";
                columns += "`" + column_name + "`";
                if (i != content.tester_status.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert
            try
            {
                for (int i = 0; i < content.tester_status.Rows.Count; i++)
                {
                    // 只存一行資料，超過就不存
                    if (i > 0) break;
                    values = "\"" + ConvertEmptyToDefaultString(device_info_Id) + "\",";
                    //values = "\"" + device_info_Id + "\",";
                    for (int j = 0; j < content.tester_status.Columns.Count; j++)
                    {
                        string columnName = content.tester_status.Columns[j].ColumnName;
                        string[] doubleTypeColumn = { "Duts", "UPH", "Avg test time", "Max test time", "Min test time", "Avg index test time", "Max index test time", "Min index test time", "Diff time (die)", "End time (die)", "First time (die)", "Diff time (file)" };
                        if (doubleTypeColumn.Contains(columnName) && (content.tester_status.Rows[0][j].ToString().Trim() == "" || content.tester_status.Rows[0][j].ToString().Trim() == "NA"))
                        {
                            values += "NULL";
                        }
                        else
                        {
                            values += "\"" + ConvertEmptyToDefaultString(content.tester_status.Rows[i][j].ToString()) + "\"";
                            //values += "\"" + content.tester_status.Rows[i][j].ToString().Trim() + "\"";
                        }
                        if (j != content.tester_status.Columns.Count - 1)
                        {
                            values += ",";
                        }
                    }
                    response = executeInsertWithAPI(webApiClient, "tester_status", columns, values);
                    if (!string.IsNullOrEmpty(response.error))
                    {
                        response = deleteTesterStatus(webApiClient, device_info_Id);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                string poolExceptionResult = PoolException(ex, webApiClient);
                Console.WriteLine(ex.ToString());
                writeToLog.writeToLog("'INSERT INTO tester_status' error:" + ex.ToString());
                response = deleteTesterStatus(webApiClient, device_info_Id);
                return false;
            }
            #endregion
            #region insert `tester_sw_version`
            columns = "`device_info_Id`,"; values = "";
            for (int i = 0; i < content.tester_sw_version.Columns.Count; i++)
            {
                string column_name = content.tester_sw_version.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                column_name = column_name.Trim().Replace(".", "_");
                if (column_name == "dct_i-v_curve_tool_md5") column_name = "dct_iv_curve_tool_md5";
                if (column_name == "simplificationui_md5") column_name = "simplification_ui_md5";
                if (column_name == "autolearn_pui_version") column_name = "auto_learn_pui_version";
                columns += "`" + column_name + "`";
                if (i != content.tester_sw_version.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert
            try
            {
                for (int i = 0; i < content.tester_sw_version.Rows.Count; i++)
                {
                    values = "\"" + ConvertEmptyToDefaultString(device_info_Id) + "\",";
                    //values = "\"" + device_info_Id + "\",";
                    for (int j = 0; j < content.tester_sw_version.Columns.Count; j++)
                    {
                        string columnName = content.tester_sw_version.Columns[j].ColumnName;
                        values += "\"" + ConvertEmptyToDefaultString(content.tester_sw_version.Rows[i][j].ToString()) + "\"";
                        //values += "\"" + content.tester_sw_version.Rows[i][j].ToString().Trim() + "\"";
                        if (j != content.tester_sw_version.Columns.Count - 1)
                        {
                            values += ",";
                        }
                    }
                    response = executeInsertWithAPI(webApiClient, "tester_sw_version", columns, values);
                    if (!string.IsNullOrEmpty(response.error))
                    {
                        response = deleteTesterStatus(webApiClient, device_info_Id);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                string poolExceptionResult = PoolException(ex, webApiClient);
                //Console.WriteLine(ex.ToString());
                writeToLog.writeToLog("'INSERT INTO tester_sw_version' error:" + ex.ToString());
                response = deleteTesterStatus(webApiClient, device_info_Id);
                return false;
            }
            #endregion
            #region insert `tester_production_analysis`
            columns = "`device_info_Id`,"; values = "";
            for (int i = 0; i < content.tester_production_analysis.Columns.Count; i++)
            {
                string column_name = content.tester_production_analysis.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                columns += "`" + column_name + "`";
                if (i != content.tester_production_analysis.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert
            try
            {
                for (int i = 0; i < content.tester_production_analysis.Rows.Count; i++)
                {
                    values = "\"" + ConvertEmptyToDefaultString(device_info_Id) + "\",";
                    //values = "\"" + device_info_Id + "\",";
                    for (int j = 0; j < content.tester_production_analysis.Columns.Count; j++)
                    {
                        string columnName = content.tester_production_analysis.Columns[j].ColumnName;
                        if (content.tester_production_analysis.Rows[0][j].ToString().Trim() == "" || content.tester_production_analysis.Rows[0][j].ToString().Trim() == "NA")
                        {
                            values += "NULL";
                        }
                        else
                        {
                            values += "\"" + ConvertEmptyToDefaultString(content.tester_production_analysis.Rows[i][j].ToString()) + "\"";
                            //values += "\"" + content.tester_production_analysis.Rows[i][j].ToString().Trim() + "\"";
                        }
                        if (j != content.tester_production_analysis.Columns.Count - 1)
                        {
                            values += ",";
                        }
                    }
                    response = executeInsertWithAPI(webApiClient, "tester_production_analysis", columns, values);
                    if (!string.IsNullOrEmpty(response.error))
                    {
                        response = deleteTesterStatus(webApiClient, device_info_Id);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                string poolExceptionResult = PoolException(ex, webApiClient);
                //Console.WriteLine(ex.ToString());
                writeToLog.writeToLog("'INSERT INTO tester_production_analysis'  error:" + ex.ToString());
                response = deleteTesterStatus(webApiClient, device_info_Id);
                return false;
            }
            #endregion
            return true;
        }
        // UI Status 匯入資料庫
        public bool importUIStatus(UIStatusContentFormat content, WebApiClient webApiClient)
        {
            if (content.UI_status.Rows.Count < 1) return false;
            // assign 需要 insert 的 欄位名稱 與 values
            string columns = "", values = "";
            Pool_excute_response response;
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
                values = "";
                for (int j = 0; j < content.UI_status.Columns.Count; j++)
                {
                    string[] numTypeColumn = { "auto_learn", "dct_product_file_setting_ui", "dct_login_ui", "os_self_diag_2k", "pattonkan_ui", "dct_i_v_curve_tool", "os_tester_100ma_vi", "os_tester_2a_vi", "os_tester_lcr_meter", "wire_assignment_tool", "bga_highlight_tool", "simplificationui", "os_scan_tool", "dct_uploadtp_ui", "dct_autodownloadtp", "dct_sw_control_tool", "dct_downloadtp_kh" };
                    if (numTypeColumn.Contains(content.UI_status.Columns[j].ColumnName.ToLower()) && (content.UI_status.Rows[i][j].ToString().Trim() == "" || content.UI_status.Rows[i][j].ToString().Trim() == "NA"))
                    {
                        values += "NULL";
                    }
                    else if (content.UI_status.Columns[j].ColumnName.ToLower() == "date" && (content.UI_status.Rows[i][j].ToString().Trim() == "" || content.UI_status.Rows[i][j].ToString().Trim() == "0"))
                    {
                        date = "null";
                        values += "NULL";
                    }
                    else
                    {
                        values += "\"" + ConvertEmptyToDefaultString(content.UI_status.Rows[i][j].ToString()) + "\"";
                        //values += "\"" + content.UI_status.Rows[i][j].ToString().Trim() + "\"";
                    }
                    if (j != content.UI_status.Columns.Count - 1)
                    {
                        values += ",";
                    }
                }
                try
                {
                    //isDataExist = isUIStatusDataExistInDB(mac_address, area, factory, os_machine, date, webApiClient);
                    //if(isDataExist)
                    //{
                    //    Console.WriteLine("資料庫已存在此資料: UI Status   mac_address=" + mac_address + ", area=" + area + ", factory=" + factory + ", os_machine=" + os_machine + ", date=" + date);
                    //}
                    //else
                    //{
                    response = executeInsertWithAPI(webApiClient, "ui_status", columns, values);
                    if (!string.IsNullOrEmpty(response.error))
                    {
                        return false;
                    }
                    //}
                }
                catch (Exception ex)
                {
                    string poolExceptionResult = PoolException(ex, webApiClient);
                    Console.WriteLine(ex.ToString());
                    return false;
                }
            }
            #endregion
            return true;
        }
        // Fail Pin Log 匯入資料庫
        public bool importFailPinLog(FailPinLogContentFormat content, WebApiClient webApiClient)
        {
            if (content.fail_pin_rate_info.Rows.Count < 1) return false;
            // assign 需要 insert 的 欄位名稱 與 values
            string columns = "", values = "";
            Pool_excute_response response;
            #region insert `fail_pin_rate_info`
            for (int i = 0; i < content.fail_pin_rate_info.Columns.Count; i++)
            {
                string column_name = content.fail_pin_rate_info.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                // 欄位名稱調整
                //if (column_name == "db_key") column_name = "db_version";
                if (!string.IsNullOrEmpty(content.fail_pin_rate_info.Rows[0][i].ToString().Trim()))
                {
                    columns += "`" + column_name.Trim() + "`";
                    values += "\"" + ConvertEmptyToDefaultString(content.fail_pin_rate_info.Rows[0][i].ToString()) + "\"";
                    //values += "\"" + content.fail_pin_rate_info.Rows[0][i].ToString().Trim() + "\"";
                }
                else
                {
                    columns = columns.Substring(0, columns.Length - 1);
                    values = values.Substring(0, values.Length - 1);
                }
                if (i != content.fail_pin_rate_info.Columns.Count - 1)
                {
                    columns += ",";
                    values += ",";
                }
            }
            try
            {
                response = executeInsertWithAPI(webApiClient, "fail_pin_rate_info", columns, values);
                if (!string.IsNullOrEmpty(response.error))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                string poolExceptionResult = PoolException(ex, webApiClient);
                Console.WriteLine(ex.ToString());
                writeToLog.writeToLog("'INSERT INTO fail_pin_rate_info' error:" + ex.ToString());
                return false;
            }
            #endregion
            string fail_pin_rate_info_Id = response.data[0]["insertId"].ToString();
            List<string> fail_pin_rate_list_Id = new List<string>();
            #region insert `fail_pin_rate_list`
            columns = "`fail_pin_rate_info_Id`,"; values = "";
            for (int i = 0; i < content.fail_pin_rate_list.Columns.Count; i++)
            {
                string column_name = content.fail_pin_rate_list.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                columns += "`" + column_name + "`";
                if (i != content.fail_pin_rate_list.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert
            try
            {
                for (int i = 0; i < content.fail_pin_rate_list.Rows.Count; i++)
                {
                    values = "\"" + ConvertEmptyToDefaultString(fail_pin_rate_info_Id) + "\",";
                    //values = "\"" + fail_pin_rate_info_Id + "\",";
                    for (int j = 0; j < content.fail_pin_rate_list.Columns.Count; j++)
                    {
                        string columnName = content.fail_pin_rate_list.Columns[j].ColumnName;
                        string[] doubleTypeColumn = { "dut", "site" };
                        if (doubleTypeColumn.Contains(columnName) && (content.fail_pin_rate_list.Rows[0][j].ToString().Trim() == "" || content.fail_pin_rate_list.Rows[0][j].ToString().Trim() == "NA"))
                        {
                            values += "NULL";
                        }
                        else
                        {
                            values += "\"" + ConvertEmptyToDefaultString(content.fail_pin_rate_list.Rows[i][j].ToString()) + "\"";
                            //values += "\"" + content.fail_pin_rate_list.Rows[i][j].ToString().Trim() + "\"";
                        }
                        if (j != content.fail_pin_rate_list.Columns.Count - 1)
                        {
                            values += ",";
                        }
                    }
                    response = executeInsertWithAPI(webApiClient, "fail_pin_rate_list", columns, values);
                    if (!string.IsNullOrEmpty(response.error))
                    {
                        response = deleteFailPinLog(webApiClient, fail_pin_rate_info_Id);
                        return false;
                    }
                    // 將此筆insert的fail_pin_rate_list_id保存至陣列
                    fail_pin_rate_list_Id.Add(response.data[0]["insertId"].ToString());
                }
            }
            catch (Exception ex)
            {
                string poolExceptionResult = PoolException(ex, webApiClient);
                Console.WriteLine(ex.ToString());
                response = deleteFailPinLog(webApiClient, fail_pin_rate_info_Id);
                return false;
            }
            #endregion
            #region insert `fail_pin_rate_list_pin_ball`
            columns = ""; values = "";
            for (int i = 0; i < content.fail_pin_rate_list_pin_ball.Columns.Count; i++)
            {
                string column_name = content.fail_pin_rate_list_pin_ball.Columns[i].ColumnName.ToLower();
                column_name = column_name.Trim().Replace(" ", "_");
                columns += "`" + column_name + "`";
                if (i != content.fail_pin_rate_list_pin_ball.Columns.Count - 1)
                {
                    columns += ",";
                }
            }
            // 開始逐一insert
            try
            {
                values = "";
                for (int i = 0; i < content.fail_pin_rate_list_pin_ball.Rows.Count; i++)
                {
                    values += "(";
                    for (int j = 0; j < content.fail_pin_rate_list_pin_ball.Columns.Count; j++)
                    {
                        string columnName = content.fail_pin_rate_list_pin_ball.Columns[j].ColumnName;
                        if (columnName != "fail_pin_rate_list_id")
                        {
                            values += "\"" + ConvertEmptyToDefaultString(content.fail_pin_rate_list_pin_ball.Rows[i][j].ToString()) + "\"";
                            //values += "\"" + content.fail_pin_rate_list_pin_ball.Rows[i][j].ToString().Trim() + "\"";
                        }
                        else
                        {
                            string val = ConvertEmptyToDefaultString(content.fail_pin_rate_list_pin_ball.Rows[i][j].ToString());
                            //string val = content.fail_pin_rate_list_pin_ball.Rows[i][j].ToString().Trim();
                            values += "\"" + fail_pin_rate_list_Id[int.Parse(val) - 1].ToString() + "\"";
                        }
                        if (j != content.fail_pin_rate_list_pin_ball.Columns.Count - 1)
                        {
                            values += ",";
                        }
                    }
                    // 每50個row就匯入一次
                    if (i != 0 && i % 50 == 0)
                    {
                        values += ")";
                        values = values.Substring(1, values.Length - 2);
                        response = executeInsertWithAPI(webApiClient, "fail_pin_rate_list_pin_ball", columns, values);
                        if (!string.IsNullOrEmpty(response.error))
                        {
                            response = deleteFailPinLog(webApiClient, fail_pin_rate_info_Id);
                            return false;
                        }
                        values = "";
                    }
                    else if (i != content.fail_pin_rate_list_pin_ball.Rows.Count - 1)
                    {
                        values += "),";
                    }
                    else
                    {
                        values += ")";
                    }
                }
                if (!string.IsNullOrEmpty(values))
                {
                    values = values.Substring(1, values.Length - 2);
                    response = executeInsertWithAPI(webApiClient, "fail_pin_rate_list_pin_ball", columns, values);
                    if (!string.IsNullOrEmpty(response.error))
                    {
                        response = deleteFailPinLog(webApiClient, fail_pin_rate_info_Id);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                string poolExceptionResult = PoolException(ex, webApiClient);
                Console.WriteLine(ex.ToString());
                response = deleteFailPinLog(webApiClient, fail_pin_rate_info_Id);
                return false;
            }
            #endregion
            #region insert `fail_pin_rate_test_result`             2024/3/1 新增
            columns = "`fail_pin_rate_list_id`,`item_name`,`open`,`short`,`vmeas`";
            // 開始逐一insert
            try
            {
                values = "";
                for (int table_i = 0; table_i < content.fail_pin_rate_list_test_result.Tables.Count; table_i++)
                {
                    for (int i = 0; i < content.fail_pin_rate_list_test_result.Tables[table_i].Rows.Count; i++)
                    {
                        values += "(\"" + ConvertEmptyToDefaultString(fail_pin_rate_list_Id[table_i].ToString()) + "\",";
                        //values += "(\"" + fail_pin_rate_list_Id[table_i].ToString() + "\",";
                        for (int j = 0; j < content.fail_pin_rate_list_test_result.Tables[table_i].Columns.Count; j++)
                        {
                            if (j > 0 && string.IsNullOrEmpty(content.fail_pin_rate_list_test_result.Tables[table_i].Rows[i][j].ToString()))
                            {
                                values += "NULL";
                            }
                            else
                            {
                                values += "\"" + ConvertEmptyToDefaultString(content.fail_pin_rate_list_test_result.Tables[table_i].Rows[i][j].ToString()) + "\"";
                                //values += "\"" + content.fail_pin_rate_list_test_result.Tables[table_i].Rows[i][j].ToString().Trim() + "\"";
                            }
                            if (j != content.fail_pin_rate_list_test_result.Tables[table_i].Columns.Count - 1)
                            {
                                values += ",";
                            }
                        }
                        // 每50個row就匯入一次
                        if (i != 0 && i % 50 == 0)
                        {
                            values += ")";
                            values = values.Substring(1, values.Length - 2);
                            response = executeInsertWithAPI(webApiClient, "fail_pin_rate_test_result", columns, values);
                            if (!string.IsNullOrEmpty(response.error))
                            {
                                response = deleteFailPinLog(webApiClient, fail_pin_rate_info_Id);
                                return false;
                            }
                            values = "";
                        }
                        else if (table_i != content.fail_pin_rate_list_test_result.Tables.Count - 1)
                        {
                            values += "),";
                        }
                        else
                        {
                            values += "),";
                        }
                    }
                }
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
                    response = executeInsertWithAPI(webApiClient, "fail_pin_rate_test_result", columns, values);
                    if (!string.IsNullOrEmpty(response.error))
                    {
                        response = deleteFailPinLog(webApiClient, fail_pin_rate_info_Id);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                string poolExceptionResult = PoolException(ex, webApiClient);
                Console.WriteLine(ex.ToString());
                response = deleteFailPinLog(webApiClient, fail_pin_rate_info_Id);
                return false;
            }
            #endregion
            return true;
        }
        public Pool_excute_response executeInsertWithAPI(WebApiClient webApiClient, string tableName, string columns, string values)
        {
            try
            {
                // 宣告 Web API body
                Pool_excute pool_excute = new Pool_excute
                {
                    pool = Program.POOL_NAME,
                    query = "INSERT INTO " + tableName + "(" + columns + ") VALUES (" + values + ");"
                };
                // 回傳 {"data":{"fieldCount":0,"affectedRows":1,"insertId":1,"info":"","serverStatus":2,"warningStatus":0},"error":null}
                Pool_excute_response response = webApiClient.ExcutePoolAsync(pool_excute, "insert").GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(response.error))
                {
                    writeToLog.writeToLog("'INSERT INTO " + tableName + "' response error:" + response.error);
                }
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }
        private Pool_excute_response deleteRawData(WebApiClient webApiClient, string lot_id)
        {
            // 宣告 Web API body
            Pool_excute pool_excute = new Pool_excute
            {
                pool = Program.POOL_NAME,
                query = @"DELETE t1, t2, t3
                                          FROM lots_info t1
                                          LEFT JOIN lots_statistic t2 ON t1.id = t2.lot_id
                                          LEFT JOIN lots_result t3 ON t1.id = t3.lot_id
                                    WHERE t1.id = " + lot_id + "; "
            };
            // 回傳 {"data":{"fieldCount":0,"affectedRows":1,"insertId":1,"info":"","serverStatus":2,"warningStatus":0},"error":null}
            Pool_excute_response response = webApiClient.ExcutePoolAsync(pool_excute, "delete").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("DELETE lots_info, lots_statistic, lots_result error: " + pool_excute.query + " " + response.error);
            }
            return response;
        }
        private Pool_excute_response deleteTesterStatus(WebApiClient webApiClient, string device_info_id)
        {
            // 宣告 Web API body
            Pool_excute pool_excute = new Pool_excute
            {
                pool = Program.POOL_NAME,
                query = @"DELETE t1, t2, t3, t4
                                          FROM tester_device_info t1
                                          LEFT JOIN tester_status t2 ON t1.id = t2.device_info_id
                                          LEFT JOIN tester_sw_version t3 ON t1.id = t3.device_info_id
                                          LEFT JOIN tester_production_analysis t4 ON t1.id = t4.device_info_id
                                     WHERE t1.id = " + device_info_id + "; "
            };
            // 回傳 {"data":{"fieldCount":0,"affectedRows":1,"insertId":1,"info":"","serverStatus":2,"warningStatus":0},"error":null}
            Pool_excute_response response = webApiClient.ExcutePoolAsync(pool_excute, "delete").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("DELETE tester_device_info, tester_status, tester_sw_version, tester_production_analysis error: " + pool_excute.query + " " + response.error);
            }
            return response;
        }
        private Pool_excute_response deleteFailPinLog(WebApiClient webApiClient, string fail_pin_id)
        {
            // 宣告 Web API body
            Pool_excute pool_excute = new Pool_excute
            {
                pool = Program.POOL_NAME,
                query = @"DELETE t3
	                                    FROM fail_pin_rate_list_pin_ball t3
	                                    LEFT JOIN fail_pin_rate_list t2 ON t2.id = t3.fail_pin_rate_list_id
                                    WHERE t2.fail_pin_rate_info_id = " + fail_pin_id + "; "
            };
            // 回傳 {"data":{"fieldCount":0,"affectedRows":1,"insertId":1,"info":"","serverStatus":2,"warningStatus":0},"error":null}
            Pool_excute_response response = webApiClient.ExcutePoolAsync(pool_excute, "delete").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("DELETE fail_pin_rate_list_pin_ball error: " + pool_excute.query + " " + response.error);
            }
            // 宣告 Web API body
            pool_excute = new Pool_excute
            {
                pool = Program.POOL_NAME,
                query = @"DELETE t4
	                                    FROM fail_pin_rate_test_result t4
	                                    LEFT JOIN fail_pin_rate_list t2 ON t2.id = t4.fail_pin_rate_list_id
                                    WHERE t2.fail_pin_rate_info_id = " + fail_pin_id + "; "
            };
            // 回傳 {"data":{"fieldCount":0,"affectedRows":1,"insertId":1,"info":"","serverStatus":2,"warningStatus":0},"error":null}
            response = webApiClient.ExcutePoolAsync(pool_excute, "delete").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("DELETE fail_pin_rate_test_result error: " + pool_excute.query + " " + response.error);
            }
            // 宣告 Web API body
            pool_excute = new Pool_excute
            {
                pool = Program.POOL_NAME,
                query = @"DELETE t1, t2
	                                      FROM fail_pin_rate_list t2
	                                      LEFT JOIN fail_pin_rate_info t1 ON t1.id = t2.fail_pin_rate_info_id
                                    WHERE t1.id = " + fail_pin_id + "; "
            };
            // 回傳 {"data":{"fieldCount":0,"affectedRows":1,"insertId":1,"info":"","serverStatus":2,"warningStatus":0},"error":null}
            response = webApiClient.ExcutePoolAsync(pool_excute, "delete").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("DELETE fail_pin_rate_list and fail_pin_rate_info error: " + pool_excute.query + " " + response.error);
            }
            return response;
        }
        public void addColumnForDataset(DataSet ds_lot_statistic, string columnName, List<StatisticItem> values)
        {
            for (int i = 0; i < ds_lot_statistic.Tables.Count; i++)
            {
                ds_lot_statistic.Tables[i].Columns.Add("avg_2", typeof(decimal));
                ds_lot_statistic.Tables[i].Columns.Add("pass_n", typeof(int));
                if (ds_lot_statistic.Tables[i].Rows.Count < 1) continue;
                ds_lot_statistic.Tables[i].Rows[0]["AVG"] = Math.Round(values[i].avg, 9);
                ds_lot_statistic.Tables[i].Rows[0]["avg_2"] = values[i].avg2;
                ds_lot_statistic.Tables[i].Rows[0]["pass_n"] = values[i].pass_n;
            }
        }
        // 判斷是否包含中文字符
        static bool IsChinese(string input)
        {
            // 使用 Unicode 字節順序標記 (BOM) 判斷字符串是否為 Unicode 編碼
            if (input.StartsWith("\uFEFF"))
            {
                input = input.Substring(1);
            }
            // 判斷字符串中是否包含中文字符
            foreach (char c in input)
            {
                if (c >= 0x4E00 && c <= 0x9FFF)
                {
                    return true;
                }
            }
            return false;
        }
        // 判斷是否為 ANSI Big5 編碼
        static bool IsBig5(string input)
        {
            // 將字符串轉換為 byte 陣列
            byte[] bytes = Encoding.Default.GetBytes(input);
            // 判斷 byte 陣列是否為 ANSI Big5 編碼
            return Encoding.GetEncoding("big5").GetString(bytes) == input;
        }
        // 判斷是否
        static bool IsUnicode(string input)
        {
            // 將字符串轉換為 byte 陣列
            byte[] bytes = Encoding.Unicode.GetBytes(input);
            // 判斷 byte 陣列是否為 Unicode 編碼
            return Encoding.Unicode.GetString(bytes) == input;
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
        private string PoolException(Exception ex, WebApiClient webApiClient)
        {
            if (ex.Message.Contains("工作已取消"))
            {
                Pool_delete pool_delete = new Pool_delete
                {
                    pool = Program.POOL_NAME
                };
                Pool_delete_response response_delete = webApiClient.DeletePoolAsync(pool_delete).GetAwaiter().GetResult();
                // 重新建立 pool
                Pool pool = new Pool
                {
                    pool_name = Program.POOL_NAME,
                    host = Program.HOST,
                    port = Program.PORT,
                    user = Program.USER,
                    password = Program.PASSWORD,
                    database = Program.DATABASE
                };
                var create_response = webApiClient.CreatePoolAsync(pool).GetAwaiter().GetResult();
                if (create_response.error != null)
                {
                    writeToLog.writeToLog("Pool 建立失敗: " + create_response.error);
                    throw new Exception("Pool 建立失敗: " + create_response.error);
                }
            }
            return "";
        }
    }
}