using System;
using DCT_data_import.Common;

namespace DCT_data_import.Test
{
    public class Phase1Test
    {
        public static void TestCommonModules()
        {
            // 代刚 FtpService
            var ftpService = new FtpService();
            var fileSize = ftpService.FormatFileSize(1024);
            
            // 代刚 StringHelper
            var result = StringHelper.ConvertEmptyToDefault("", "default");
            var columnName = StringHelper.NormalizeColumnName("DB Key");
            
            // 代刚 DatabaseHelper
            var dbHelper = new DatabaseHelper();
            var sql = dbHelper.BuildInsertSql("test_table", "id,name", "1,'test'");
            
            Console.WriteLine("Phase 1 家舱代刚ЧΘ");
        }
    }
}