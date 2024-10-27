using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            public int UiStatus { get; set; }
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
            public DbKeyObject(int id, string dbKey, int tester, int testResult, int failPin, int checkStatus)
            {
                Id = id;
                DbKey = dbKey;
                Tester = tester;
                TestResult = testResult;
                FailPin = failPin;
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
        public class Pool
        {
            public string Pool_name { get; set; }
            public string Host { get; set; }
            public string Port { get; set; }
            public string User { get; set; }
            public string Password { get; set; }
            public string Database { get; set; }
        }
        public class Pool_signin
        {
            public string UserName { get; set; }
            public string Password { get; set; }
        }
        public class Pool_execute
        {
            public string Pool { get; set; }
            public string Query { get; set; }
        }
        public class Pool_delete
        {
            public string Pool { get; set; }
        }
        public class Signin_response
        {
            public string Token { get; set; }
            public JObject User { get; set; }
        }
        public class Pool_create_response
        {
            public JValue Data { get; set; }
            public string Error { get; set; }
        }
        public class Pool_get_all_response
        {
            public JObject Data { get; set; }
            public string Error { get; set; }
        }
        public class Pool_execute_response
        {
            public JArray Data { get; set; }
            public string Error { get; set; }
        }
        public class Pool_delete_response
        {
            public JValue Data { get; set; }
            public string Error { get; set; }
        }
    }
}