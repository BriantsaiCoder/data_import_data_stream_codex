using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DCT_data_import
{
    class WriteToLog
    {
        int LogCount = 100;
        int WritedCount = 0;
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

    }
}
