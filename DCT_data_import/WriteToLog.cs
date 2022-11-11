using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DCT_data_import
{
    class WriteToLog
    {
        public void writeToLog(string message)
        {
            //string log_path = @"C:\temp\HL_System_WEB_Log.txt";
            string log_path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + @"\DCT_data_import_Log.txt").LocalPath;
            if (!File.Exists(log_path))
            {
                //File.Create(log_path);
                using (StreamWriter writer = File.CreateText(log_path))
                {
                    writer.WriteLine(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss") + " " + message);
                }
                return;
            }
            // Write file using StreamWriter  
            using (StreamWriter writer = File.AppendText(log_path))
            {
                writer.WriteLine(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss") + " " + message);
            }
        }

    }
}
