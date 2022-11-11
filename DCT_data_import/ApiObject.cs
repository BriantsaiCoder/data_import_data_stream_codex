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
