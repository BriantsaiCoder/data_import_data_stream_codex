using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DCT_data_import.ApiObject;

namespace DCT_data_import
{
    class CompareTool
    {
        //public static string POOL_NAME = "DB_program";
        //public static string HOST = "192.168.0.105";
        //public static string PORT = "3308";
        //public static string USER = "5910";
        //public static string PASSWORD = "5910";
        //public static string DATABASE = "dct_test";


        public static string POOL_NAME = "dct_import";
        public static string HOST = "192.168.0.101";
        public static string PORT = "3306";
        public static string USER = "5940";
        public static string PASSWORD = "5940";
        public static string DATABASE = "dct";

        public bool compareRawData(RawDataContentFormat content, WebApiClient webApiClient)
        {
             if (content == null || content.lotInfo.Rows.Count < 1) return false;
            string db_key = content.lotInfo.Rows[0]["DB_Key"].ToString();
            WriteToLog writeToLog = new WriteToLog();
            Pool_excute pool_excute;
            Pool_excute_response response;
            string column_name;
            string info_Id;


            CalculateSPC calculateSPC = new CalculateSPC();
            List<StatisticItem>  avg_2 = calculateSPC.AverageOfSumSquare(content);


            // 查詢 lots_info
            pool_excute = new Pool_excute
            {
                pool = POOL_NAME,
                query = "SELECT * FROM `lots_info` WHERE `db_key` = '" + db_key + "';"
            };
            response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("SELECT  `lots_info` response error:" + response.error);
                return false;
            }
            if (response.data.Count < 1) return false;
            info_Id = response.data[0]["id"].ToString();

            for (int i = 0; i < content.lotInfo.Rows.Count; i++)
            {
                for (int j = 0; j < content.lotInfo.Columns.Count; j++)
                {
                    column_name = content.lotInfo.Columns[j].ToString().ToLower().Trim().Replace(" ", "_");
                    column_name = column_name.Split('(', ')')[0];
                    // 欄位名稱調整
                    if (column_name == "bondingdiagram") column_name = "bonding_diagram";
                    // 不比對
                    if (column_name == "start" || column_name == "stop") continue;

                    string val1 = content.lotInfo.Rows[i][j].ToString();
                    string val2 = response.data[i][column_name].ToString();

                    if (val1 != val2)
                    {
                        if (column_name == "yield" || column_name == "total_ppm"|| column_name == "open_pin_fail_ppm" || column_name == "short_pin_fail_ppm" || column_name == "leakage_pin_fail_ppm")
                        {
                            if (double.Parse(val1) != double.Parse(val2))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }


            // 查詢 lots_statistic
            pool_excute = new Pool_excute
            {
                pool = POOL_NAME,
                query = "SELECT * FROM `lots_statistic` WHERE `lot_id` = '" + info_Id + "';"
            };
            response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("SELECT  `lots_statistic` response error:" + response.error);
                return false;
            }
            if (content.lotStatistic.Tables.Count < 1 && response.data.Count < 1) return true;

            for (int i = 0; i < content.lotStatistic.Tables.Count; i++)
            {
                for (int j = 0; j < content.lotStatistic.Tables[i].Columns.Count; j++)
                {
                    column_name = content.lotStatistic.Tables[i].Columns[j].ToString().ToLower().Trim().Replace(" ", "_");
                    // 欄位名稱調整
                    if (column_name == "#_of_pass") column_name = "pass";
                    if (column_name == "#_of_fail") column_name = "fail";
                    // 不比對
                    if (column_name == "real_time" || column_name == "cp" || column_name == "cpk" || column_name == "sum_of_square") continue;

                    string val1 = content.lotStatistic.Tables[i].Rows[0][j].ToString();
                    string val2 = response.data[i][column_name].ToString();

                    if (val1 != val2)
                    {
                        if (column_name == "force" || column_name == "wait_time" || column_name == "cpk" || column_name == "cp"|| column_name == "min" || column_name == "max" || column_name == "stdev")
                        {
                            double double_val1 = Math.Round(double.Parse(val1), 8);
                            double double_val2 = Math.Round(double.Parse(val2), 8);
                            if (double_val1 != double_val2 && Math.Abs(double_val1- double_val2) > 0.0000002)
                            {
                                return false;
                            }
                        }
                        else if (column_name == "value")
                        {
                            val1 = val1.Trim().Replace(" ", "").Replace("[", "").Replace("]", "");
                            double[] double_val1 = Array.ConvertAll(val1.Split(new char[] { ',' }), Double.Parse);
                            val2 = val2.Trim().Replace("\r\n ", "").Replace("\r\n", "").Replace(" ", "").Replace("[", "").Replace("]", "");
                            double[] double_val2 = Array.ConvertAll(val2.Split(new char[] { ',' }), Double.Parse);
                            for(int idx = 0; idx < double_val1.Length;idx++)
                            {
                                if (Math.Round(double_val1[idx], 8) != Math.Round(double_val2[idx], 8))
                                {
                                    return false;
                                }
                            }
                            //if (double_val1 != double_val2)
                            //{
                            //    return false;
                            //}
                        }
                        else if(column_name == "avg")
                        {
                            if (avg_2[i].avg != decimal.Parse(val2))
                            {
                                return false;
                            }
                            val2 = response.data[i]["avg_2"].ToString();
                            if(avg_2[i].avg2 != decimal.Parse(val2))
                            {
                                return false;
                            }

                        }
                        else if (column_name == "avg_2")
                        {
                            if (avg_2[i].avg2 != decimal.Parse(val2))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            Console.WriteLine("val1: " + val1);
                            Console.WriteLine("val2: " + val2);
                            return false;
                        }
                    }
                }
            }
            

            // 查詢 lots_result
            pool_excute = new Pool_excute
            {
                pool = POOL_NAME,
                query = "SELECT * FROM `lots_result` WHERE `lot_id` = '" + info_Id + "';"
            };
            response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("SELECT  `lots_result` response error:" + response.error);
                return false;
            }

            for (int i = 0; i < content.lotResult.Rows.Count; i++)
            {
                for (int j = 0; j < content.lotResult.Columns.Count; j++)
                {
                    column_name = content.lotResult.Columns[j].ToString().ToLower().Trim().Replace(" ", "_");
                    // 欄位名稱調整
                    if (column_name == "siteid") column_name = "site_id";
                    if (column_name == "p/f") column_name = "pass/fail";
                    // 不比對
                    if (column_name == "real_time") continue;

                    string val1 = content.lotResult.Rows[i][j].ToString();
                    string val2 = response.data[i][column_name].ToString();

                    if (val1 != val2)
                    {
                        if (column_name == "test_time" || column_name == "index_time" )
                        {
                            double double_val1 = Math.Round(double.Parse(val1), 8);
                            double double_val2 = Math.Round(double.Parse(val2), 8);
                            if (Math.Abs(double_val1 - double_val2) > 0.00000002)
                            {
                                return false;
                            }
                        }
                        else if(column_name == "pass/fail")
                        {
                            if (val1.ToLower() != val2)
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public bool compareTesterStatus(TestStatusContentFormat content, WebApiClient webApiClient)
        {
            if (content == null || content.tester_device_info.Rows.Count < 1) return false;
            string db_key = content.tester_device_info.Rows[0]["DB_Key"].ToString();
            WriteToLog writeToLog = new WriteToLog();
            Pool_excute pool_excute;
            Pool_excute_response response;
            string column_name;
            string info_Id;

            // 查詢 tester_device_info
            pool_excute = new Pool_excute
            {
                pool = POOL_NAME,
                query = "SELECT * FROM `tester_device_info` WHERE `db_key` = '" + db_key + "';"
            };
            response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("SELECT  `tester_device_info` response error:" + response.error);
                return false;
            }
            if (response.data.Count < 1) return false;
            info_Id = response.data[0]["id"].ToString();

            for (int i = 0; i < content.tester_device_info.Rows.Count; i++)
            {
                for (int j = 0; j < content.tester_device_info.Columns.Count; j++)
                {
                    column_name = content.tester_device_info.Columns[j].ToString().ToLower().Trim().Replace(" ", "_");
                    // 欄位名稱調整
                    if (column_name == "prober_/_handler") column_name = "prober/handler";
                    if (column_name == "l/b_id") column_name = "L/B_id";
                    // 不比對
                    if (column_name == "start_time"|| column_name == "end_time") continue;


                    if (content.tester_device_info.Rows[i][j].ToString() != response.data[i][column_name].ToString())
                    {
                        if (column_name == "yield")
                        {
                            if (content.tester_device_info.Rows[i][j].ToString() == "NA" && string.IsNullOrEmpty(response.data[i][column_name].ToString()))
                            {
                                continue;
                            }
                            else if (double.Parse(content.tester_device_info.Rows[i][j].ToString()) != double.Parse(response.data[i][column_name].ToString()))
                            {
                                string val1 = content.tester_device_info.Rows[i][j].ToString();
                                string val2 = response.data[i][column_name].ToString();
                                return false;
                            }
                        }
                        else
                        {
                            string val1 = content.tester_device_info.Rows[i][j].ToString();
                            string val2 = response.data[i][column_name].ToString();
                            return false;
                        }
                    }
                }
            }

            // 查詢 tester_status
            pool_excute = new Pool_excute
            {
                pool = POOL_NAME,
                query = "SELECT * FROM `tester_status` WHERE `device_info_id` = '" + info_Id + "';"
            };
            response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("SELECT `tester_status` response error:" + response.error);
                return false;
            }
            for (int i = 0; i < 1; i++)
            {
                for (int j = 0; j < content.tester_status.Columns.Count; j++)
                {
                    column_name = content.tester_status.Columns[j].ToString().ToLower().Trim().Replace(" ", "_");
                    if (column_name == "diff_time_(die)") column_name = "diff_time_die";
                    if (column_name == "end_time_(die)") column_name = "end_time_die";
                    if (column_name == "first_time_(die)") column_name = "first_time_die";
                    if (column_name == "diff_time_(file)") column_name = "diff_time_file";
                    if (column_name == "pass_/_fail") column_name = "pass/fail";

                    string val1 = content.tester_status.Rows[i][j].ToString();
                    string val2 = response.data[i][column_name].ToString();

                    if (val1 != val2)
                    {
                        if (string.IsNullOrEmpty(val2))
                        {
                            if (val1 != "NA")
                            {
                                return false;
                            }
                        }
                        else if (column_name == "pass/fail")
                        {
                            if(val1.ToLower() != val2)
                            {
                                return false;
                            }
                        }
                        else if (column_name == "uph" )
                        {
                            if (Math.Round(double.Parse(val1), 8) != Math.Round(double.Parse(val2), 8))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }

            // 查詢 tester_sw_version
            pool_excute = new Pool_excute
            {
                pool = POOL_NAME,
                query = "SELECT * FROM `tester_sw_version` WHERE `device_info_id` = '" + info_Id + "';"
            };
            response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("SELECT  `tester_sw_version` response error:" + response.error);
                return false;
            }

            for (int i = 0; i < content.tester_sw_version.Rows.Count; i++)
            {
                for (int j = 0; j < content.tester_sw_version.Columns.Count; j++)
                {
                    column_name = content.tester_sw_version.Columns[j].ToString().ToLower().Trim().Replace(" ", "_");
                    column_name = column_name.Trim().Replace(".", "_");
                    if (column_name == "dct_i-v_curve_tool_md5") column_name = "dct_iv_curve_tool_md5";
                    if (column_name == "simplificationui_md5") column_name = "simplification_ui_md5";
                    if (column_name == "autolearn_pui_version") column_name = "auto_learn_pui_version";
                    
                    string val1 = content.tester_sw_version.Rows[i][j].ToString();
                    string val2 = response.data[i][column_name].ToString();

                    if (val1 != val2)
                    {
                        if (string.IsNullOrEmpty(val2))
                        {
                            if (val1 != "NA")
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }


            // 查詢 tester_production_analysis
            pool_excute = new Pool_excute
            {
                pool = POOL_NAME,
                query = "SELECT * FROM `tester_production_analysis` WHERE `device_info_id` = '" + info_Id + "';"
            };
            response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("SELECT  `tester_production_analysis` response error:" + response.error);
                return false;
            }

            for (int i = 0; i < content.tester_production_analysis.Rows.Count; i++)
            {
                for (int j = 0; j < content.tester_production_analysis.Columns.Count; j++)
                {
                    column_name = content.tester_production_analysis.Columns[j].ToString().ToLower().Trim().Replace(" ", "_");

                    string val1 = content.tester_production_analysis.Rows[i][j].ToString();
                    string val2 = response.data[i][column_name].ToString();

                    if (val1 != val2)
                    {
                        if (string.IsNullOrEmpty(val2))
                        {
                            if (val1 != "NA")
                            {
                                return false;
                            }
                        }
                        else
                        {
                            if (double.Parse(val1) != double.Parse(val2))
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

   

        public bool compareFailPinLog(FailPinLogContentFormat content, WebApiClient webApiClient)
        {
            if (content == null || content.fail_pin_rate_info.Rows.Count < 1) return false;

            string db_key = content.fail_pin_rate_info.Rows[0]["DB Key"].ToString();
            WriteToLog writeToLog = new WriteToLog();
            Pool_excute pool_excute;
            Pool_excute_response response;
            string column_name;
            List<string> fail_pin_rate_list_Id = new List<string>();

            // 查詢 fail_pin_rate_info
            pool_excute = new Pool_excute
            {
                pool = POOL_NAME,
                query = "SELECT * FROM `fail_pin_rate_info` WHERE `db_key` = '"+ db_key + "';"
            };
            response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("SELECT  `fail_pin_rate_info` response error:" + response.error);
                return false;
            }
            if (response.data.Count <1) return false;
            string fail_pin_rate_info_Id = response.data[0]["id"].ToString();

            for (int i = 0; i < content.fail_pin_rate_info.Rows.Count; i++)
            {
                for (int j = 0; j < content.fail_pin_rate_info.Columns.Count; j++)
                {
                    column_name = content.fail_pin_rate_info.Columns[j].ToString().ToLower().Trim().Replace(" ", "_");
                    // 日期不比對
                    if (column_name == "date") continue;
                    if (content.fail_pin_rate_info.Rows[i][j].ToString() != response.data[i][column_name].ToString()) return false;
                }
            }


            // 查詢 fail_pin_rate_list
            pool_excute = new Pool_excute
            {
                pool = POOL_NAME,
                query = "SELECT * FROM `fail_pin_rate_list` WHERE `fail_pin_rate_info_id` = '"+ fail_pin_rate_info_Id + "';"
            };
            response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("SELECT `fail_pin_rate_list` response error:" + response.error);
                return false;
            }

            for (int i = 0; i < content.fail_pin_rate_list.Rows.Count; i++)
            {
                for (int j = 0; j < content.fail_pin_rate_list.Columns.Count; j++)
                {
                    column_name = content.fail_pin_rate_list.Columns[j].ToString().ToLower().Trim().Replace(" ", "_");
                    if (content.fail_pin_rate_list.Rows[i][j].ToString() != response.data[i][column_name].ToString())
                    {
                        return false;
                    }
                    // 將此筆的fail_pin_rate_list_id保存至陣列
                    fail_pin_rate_list_Id.Add(response.data[0]["id"].ToString());
                }
            }

            // 查詢 fail_pin_rate_list_pin_ball
            pool_excute = new Pool_excute
            {
                pool = POOL_NAME,
                query = @"SELECT * FROM fail_pin_rate_list_pin_ball t3
                                        LEFT JOIN fail_pin_rate_list t2 ON t2.id = t3.fail_pin_rate_list_id
                                        WHERE t2.fail_pin_rate_info_id = '"+ fail_pin_rate_info_Id + "'"
            };
            response = webApiClient.ExcutePoolAsync(pool_excute, "select").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(response.error))
            {
                writeToLog.writeToLog("SELECT `fail_pin_rate_list_pin_ball` response error:" + response.error);
                return false;
            }
            for (int i = 0; i < content.fail_pin_rate_list_pin_ball.Rows.Count; i++)
            {
                for (int j = 0; j < content.fail_pin_rate_list_pin_ball.Columns.Count; j++)
                {
                    column_name = content.fail_pin_rate_list_pin_ball.Columns[j].ToString().ToLower().Trim().Replace(" ", "_");
                    if (column_name == "fail_pin_rate_list_id")
                    {
                        continue;
                    }
                    if (content.fail_pin_rate_list_pin_ball.Rows[i][j].ToString() != response.data[i][column_name].ToString())
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
