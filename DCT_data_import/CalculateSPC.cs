using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT_data_import
{
    public class StatisticItem
    {
        public decimal pass_n { get; set; }
        public decimal avg { get; set; }
        public decimal avg2 { get; set; }
    }

    class CalculateSPC
    {

        public List<double> SquareSum(RawDataContentFormat content)
        {
            List<double> list_square_sum = new List<double>();
            double sum_of_square;
            double value;
            string str_values;
            string[] split_str;
            List<double> list_avg = new List<double>();
            List<double> list_stdev = new List<double>();
            List<int> list_n = new List<int>();
            double sum, avg, stdev;
            int N;

            for (int i = 0; i < content.lotStatistic.Tables.Count; i++)
            {
                if (content.lotStatistic.Tables[i].Rows.Count < 1) continue;
                str_values = content.lotStatistic.Tables[i].Rows[0]["value"].ToString();
                split_str = str_values.Split(new char[] { '[', ']', ',' });
                sum_of_square = 0;
                sum = 0;
                N = 0;

                for (int j = 0; j < split_str.Length; j++)
                {
                    if(double.TryParse(split_str[j], out value))
                    {
                        sum_of_square += value * value;
                        sum += value;
                        N++;
                    }
                }
                avg = sum / N;
                if(sum_of_square / N - avg * avg<0)
                {
                    Console.WriteLine("發現負數根號值!");
                }
                stdev = Math.Sqrt(sum_of_square / N - avg * avg);

                list_square_sum.Add(sum_of_square);
                list_avg.Add(avg);
                list_n.Add(N);
                list_stdev.Add(stdev);
            }
            
            return list_square_sum;
        }


        public List<StatisticItem> AverageOfSumSquare(RawDataContentFormat content)
        {
            List<decimal> list_avg_sum_square = new List<decimal>();
            decimal sum_of_square;
            double spec_max = 0, spec_min = 0;
            int fail_n = 0;
            string str_values;
            string[] split_str;
            List<StatisticItem> list_statistic_item = new List<StatisticItem>();
            List<double> list_values = new List<double>();
            List<decimal> list_avg = new List<decimal>();
            List<decimal> list_stdev = new List<decimal>();
            List<decimal> list_n = new List<decimal>();
            decimal sum, stdev;
            decimal avg;
            decimal N;

            try
            {
                for (int i = 0; i < content.lotStatistic.Tables.Count; i++)
                {
                    //Console.Write(i.ToString() + ",");
                    //if (i == 61)
                    //{
                    //    Console.WriteLine("");
                    //}

                    if (content.lotStatistic.Tables[i].Rows.Count < 1) continue;
                    str_values = content.lotStatistic.Tables[i].Rows[0]["value"].ToString();
                    split_str = str_values.Split(new char[] { '[', ']', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    sum_of_square = 0;
                    sum = 0;
                    N = 0;
                    // 取得 fail 個數
                    if (!int.TryParse(content.lotStatistic.Tables[i].Rows[0]["# of FAIL"].ToString(), out fail_n))
                    {
                        fail_n = 0;
                    }
                    // 若 fail 數>0 先把fail的值篩掉
                    if (fail_n != 0)
                    {
                        if (!double.TryParse(content.lotStatistic.Tables[i].Rows[0]["Spec MAX"].ToString(), out spec_max))
                        {
                            spec_max = 0;
                        }
                        if (!double.TryParse(content.lotStatistic.Tables[i].Rows[0]["Spec MIN"].ToString(), out spec_min))
                        {
                            spec_min = 0;
                        }
                        list_values = seperatePassValue(split_str, spec_max, spec_min, fail_n);
                    }
                    // fail 數為零，則不用篩選
                    else
                    {
                        list_values = new List<double>();
                        for (int str_id = 0; str_id < split_str.Length; str_id++)
                        {
                            double out_d;
                            if (double.TryParse(split_str[str_id], out out_d))
                            {
                                list_values.Add(out_d);
                            }
                        }
                        //list_values = Array.ConvertAll(split_str, Double.Parse).ToList();
                    }

                    if (list_values.Count == 0)
                    {
                        list_statistic_item.Add(new StatisticItem
                        {
                            pass_n = 0,
                            avg = 0,
                            avg2 = 0,
                        });
                        continue;
                    }

                    for (int j = 0; j < list_values.Count; j++)
                    {
                        sum_of_square += Convert.ToDecimal(list_values[j] * list_values[j]);
                        sum += Convert.ToDecimal(list_values[j]);
                        N++;
                    }
                    avg = sum / N;
                    if (sum_of_square / N - avg * avg < 0)
                    {
                        Console.WriteLine("發現根號負值!");
                    }
                    stdev = Convert.ToDecimal(Math.Sqrt(Convert.ToDouble(sum_of_square / N - avg * avg)));

                    list_avg_sum_square.Add(sum_of_square / N);
                    list_avg.Add(avg);
                    list_n.Add(N);
                    list_stdev.Add(stdev);
                    list_statistic_item.Add(new StatisticItem
                    {
                        pass_n = N,
                        avg = avg,
                        avg2 = sum_of_square / N,
                    });
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("AverageOfSumSquare() error: " +ex.ToString());
            }

            
            return list_statistic_item;
        }


        private List<double> seperatePassValue(string[] values, double spec_max, double spec_min, int fail_n)
        {
            List<double> double_values = new List<double>();// Array.ConvertAll(values, Double.Parse).ToList();
            double out_val;
            for(int i =0;i< values.Length;i++)
            {
                if(double.TryParse(values[i], out out_val))
                {
                    double_values.Add(out_val);
                }
            }


            int fail_count = 0;
            List<double> double_values_test = double_values;

            //Stopwatch sw = new Stopwatch();
            //TimeSpan ts2;
            //sw.Start();

            for (int i = double_values.Count - 1; i >= 0; i--)
            {
                if (double_values[i] > spec_max || double_values[i] < spec_min)
                {
                    double_values.RemoveAt(i);
                    fail_count++;
                }
                if (fail_count == fail_n) break;
            }

            //sw.Stop();
            //ts2 = sw.Elapsed;
            //Console.WriteLine("一般拆解耗時:" + ts2.TotalMilliseconds);


            //sw.Start();

            //double_values_test.Sort();  // 由小到大
            //for (int i = double_values_test.Count - 1; i >= 0; i--)
            //{
            //    if (double_values_test[i] > spec_max)
            //    {
            //        double_values_test.RemoveAt(i);
            //        fail_count++;
            //    }
            //    else
            //    {
            //        break;
            //    }
            //}

            //if (fail_count < fail_n)
            //{
            //    double_values_test.Reverse();
            //    for (int i = double_values_test.Count - 1; i >= 0; i--)
            //    {
            //        if (double_values_test[i] < spec_min)
            //        {
            //            double_values_test.RemoveAt(i);
            //            fail_count++;
            //        }
            //        else
            //        {
            //            break;
            //        }
            //    }
            //}

            //sw.Stop();
            //ts2 = sw.Elapsed;
            //Console.WriteLine("排序拆解耗時:" + ts2.TotalMilliseconds);

            return double_values;
        }

    }
}
