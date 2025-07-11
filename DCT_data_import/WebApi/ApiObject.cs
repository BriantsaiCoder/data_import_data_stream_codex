using Newtonsoft.Json.Linq;
namespace DCT_data_import
{
    public class ApiObject
    {
        public class ImportResult
        {
            public int Result { get; set; }
            public string Message { get; set; }
            public ImportResult(int result, string messege)
            {
                Result = result;
                Message = messege;
            }
        }
        public class DbKeyObject
        {
            public int Id { get; set; }
            public string DbKey { get; set; }
            public int RecoveryRate { get; set; }
            public int Tester { get; set; }
            public int TestResult { get; set; }
            public int FailPin { get; set; }
            public int CheckStatus { get; set; }
            public string Remark { get; set; }
            public DbKeyObject(string dbKey, string remark)
            {
                DbKey = dbKey;
                CheckStatus = 0;
                Remark = remark;
            }
            public DbKeyObject(int id, string dbKey, string remark)
            {
                Id = id;
                DbKey = dbKey;
                CheckStatus = 0;
                Remark = remark;
            }
            public DbKeyObject(int id, string dbKey, int checkStatus)
            {
                Id = id;
                DbKey = dbKey;
                CheckStatus = checkStatus;
                Remark = "";
            }
            public DbKeyObject(int id, string dbKey, int recoveryRate, int tester, int testResult, int failPin, int checkStatus)
            {
                Id = id;
                DbKey = dbKey;
                RecoveryRate = recoveryRate;
                Tester = tester;
                TestResult = testResult;
                FailPin = failPin;
                CheckStatus = checkStatus;
                Remark = "";
            }
        }
        public class Pool_execute
        {
            public string Pool { get; set; }
            public string Query { get; set; }
        }
        public class Pool_execute_response
        {
            public JArray Data { get; set; }
            public string Error { get; set; }
        }
    }
}