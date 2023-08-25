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
            public int result { get; set; }
            public string messege { get; set; }

            public ImportResult(int result, string messege)
            {
                this.result = result;
                this.messege = messege;
            }

        }

        public class DbKeyObject
        {
            public int id { get; set; }
            public string dbKey { get; set; }
            public int tester { get; set; }
            public int testResult { get; set; }
            public int failPin { get; set; }
            public int uiStatus { get; set; }
            public int checkStatus { get; set; }
            public string remark { get; set; }

            public DbKeyObject(string dbKey, string remark)
            {
                this.dbKey = dbKey;
                this.checkStatus = 0;
                this.remark = remark;
            }
            public DbKeyObject(int id, string dbKey, string remark)
            {
                this.id = id;
                this.dbKey = dbKey;
                this.checkStatus = 0;
                this.remark = remark;
            }
            public DbKeyObject(int id, string dbKey, int checkStatus)
            {
                this.id = id;
                this.dbKey = dbKey;
                this.checkStatus = checkStatus;
                this.remark = "";
            }

            public DbKeyObject(int id, string dbKey, int tester, int testResult, int failPin, int checkStatus)
            {
                this.id = id;
                this.dbKey = dbKey;
                this.tester = tester;
                this.testResult = testResult;
                this.failPin = failPin;
                this.checkStatus = checkStatus;
                this.remark = "";
            }
        }


        public class Pool
        {
            public string pool_name { get; set; }
            public string host { get; set; }
            public string port { get; set; }
            public string user { get; set; }
            public string password { get; set; }
            public string database { get; set; }
        }

        public class Pool_signin
        {
            public string username { get; set; }
            public string password { get; set; }
        }


        public class Pool_excute
        {
            public string pool { get; set; }
            public string query { get; set; }
        }


        public class Pool_delete
        {
            public string pool { get; set; }
        }

        public class Signin_response
        {
            public string token { get; set; }
            public JObject user { get; set; }
        }


        public class Pool_create_response
        {
            public JValue data { get; set; }
            public string error { get; set; }
        }

        public class Pool_get_all_response
        {
            public JObject data { get; set; }
            public string error { get; set; }
        }

        public class Pool_excute_response
        {
            public JArray data { get; set; }
            public string error { get; set; }
        }


        public class Pool_delete_response
        {
            public JValue data { get; set; }
            public string error { get; set; }
        }
    }
}
