using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static DCT_data_import.ApiObject;

namespace DCT_data_import
{
    public class WriteToLog
    {
        int FailedCount = 0;

        public void writeToLog(string message)
        {
            //string log_path = @"C:\temp\HL_System_WEB_Log.txt";
            string log_path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + @"\DCT_data_import_Log.txt").LocalPath;

            //using (var mutex = new Mutex(false, log_path.Replace("\\", "")))
            //{
            //    var hasHandle = false;
                try
                {
                    //hasHandle = mutex.WaitOne(Timeout.Infinite, false);

                    if (!File.Exists(log_path))
                    {
                        //File.Create(log_path);
                        using (StreamWriter writer = File.CreateText(log_path))
                        {
                            writer.WriteLine(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss") + " " + message);
                        }
                        return;
                    }
                    //File.AppendAllText(log_path, DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss") + " " + message);
                    // Write file using StreamWriter  
                    using (StreamWriter writer = File.AppendText(log_path))
                    {
                    writer.WriteLineAsync(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss") + " " + message);
                    }
                }
                catch (Exception ex)
                {
                    FailedCount++;
                    Console.WriteLine(ex.Message);
                }
            //    finally
            //    {
            //        if (hasHandle)
            //            mutex.ReleaseMutex();
            //    }
            //}

        }


        public string WriteErrorLog(List<DbKeyObject> failDbKeyObject)
        {
            string text = "";

            string path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + @"\DCT_error_file_Log.txt").LocalPath;

            using (StreamWriter writetext = File.CreateText(path))
            {
                for (int i = 0; i < failDbKeyObject.Count; i++)
                {
                    writetext.WriteLine((i + 1).ToString() + ".    DB_Key:" + failDbKeyObject[i].dbKey + ",       " + failDbKeyObject[i].remark);
                }
            }

            return path;
        }

        public string WriteToMailTemp(string message)
        {
            string log_path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + @"\mail_temp.txt").LocalPath;

            if (!File.Exists(log_path))
            {
                //File.Create(log_path);
                using (StreamWriter writer = File.CreateText(log_path))
                {
                    writer.WriteLine(message);
                }

                return "";
            }

            // Write file using StreamWriter  
            using (StreamWriter writer = new StreamWriter(log_path, true, System.Text.Encoding.UTF8))
            {
                writer.WriteLine(message);
            }

            return "";
        }
        
        public void writeToCheckLog(string logFilename,string content)
        {
            string checkLogFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + "\\" + "check_logs";

            checkLogFolder = checkLogFolder.Substring(6);

            if (!Directory.Exists(checkLogFolder))
            {
                Directory.CreateDirectory(checkLogFolder);
            }

            string log_path = new Uri(checkLogFolder + "\\"+ logFilename).LocalPath;
            
            try
            {
                if (!File.Exists(log_path))
                {
                    using (StreamWriter writer = File.CreateText(log_path))
                    {
                        writer.WriteLine("File Name, File Size, Time, Read File Takes Time, Import Takes Time");
                        writer.WriteLine(content);
                    }
                    return;
                }
                using (StreamWriter writer = File.AppendText(log_path))
                {
                    writer.WriteLine(content);
                }
            }
            catch (Exception ex)
            {
                FailedCount++;
                Console.WriteLine(ex.Message);
            }

        }


    }
}
