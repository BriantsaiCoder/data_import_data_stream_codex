using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DCT_data_import
{
    class ReadWriteINIfile
    {
        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string name, string key, string val, string filePath);
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public string path;

        public ReadWriteINIfile(string inipath)
        {
            path = inipath;
        }
        public void WriteINI(string name, string key, string value)
        {
            WritePrivateProfileString(name, key, value, this.path);
        }
        public string ReadINI(string name, string key)
        {
            StringBuilder sb = new StringBuilder(255);
            int ini = GetPrivateProfileString(name, key, "", sb, 255, this.path);
            return sb.ToString();
        }
    }
}
