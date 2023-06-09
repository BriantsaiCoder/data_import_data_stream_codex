using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using static DCT_data_import.ApiObject;

namespace DCT_data_import
{
    public class WebApiClient
    {
        public HttpClient client = new HttpClient();
        private string api_key { get; set; }
        private string api_value { get; set; }

        public WebApiClient()
        {
            client.BaseAddress = new Uri(ConfigurationManager.ConnectionStrings["ApiUrl"].ConnectionString);
            api_key = "authorization";
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<Signin_response> getApiKeyValueAsync(Pool_signin pool_Signin)
        {
            string path = string.Format("/signin");

            // 將 data 轉為 json
            string json = JsonConvert.SerializeObject(pool_Signin);
            // 將轉為 string 的 json 依編碼並指定 content type 存為 httpcontent
            HttpContent contentPost = new StringContent(json, Encoding.UTF8, "application/json");
            // 發出 post 並取得結果
            HttpResponseMessage response = await client.PostAsync(path, contentPost).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            Signin_response result;
            if (response.IsSuccessStatusCode)
            {
                // 將回應結果內容取出並轉為 string 再透過 linqpad 輸出
                string result_str = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                result = await response.Content.ReadAsAsync<Signin_response>();

                if (result != null)
                {
                    api_value = result.token;

                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Add(api_key, api_value);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                }

                return result;
            }
            else
            {
                return null;
            }
        }

        public async Task<Pool_get_all_response> GetPoolAsync(string api_key="")
        {
            string path = string.Format("/api/mysql/pools/get-all");
            //client.DefaultRequestHeaders.Add("api-key", api_key);
            HttpResponseMessage response = await client.GetAsync(path).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                // 取得response的資料
                string result_str = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                //Console.WriteLine("get-all pool: " + result_str);
                //正確回傳: result_str = {"data":{"pool_szie":1,"pool_name":["actdata"]},"error":null}
                Pool_get_all_response result = await response.Content.ReadAsAsync<Pool_get_all_response>();
                return result;
            }else
            {
                return null;
            }
        }

        public async Task<Pool_create_response> CreatePoolAsync(Pool pool, string api_key="")
        {
            string path = string.Format("api/mysql/pools");
            // 將 data 轉為 json
            string json = JsonConvert.SerializeObject(pool);
            // 將轉為 string 的 json 依編碼並指定 content type 存為 httpcontent
            HttpContent contentPost = new StringContent(json, Encoding.UTF8, "application/json");
            //client.DefaultRequestHeaders.Add("api-key", api_key);
            HttpResponseMessage response = await client.PostAsync(path, contentPost).ConfigureAwait(false);
            //response.EnsureSuccessStatusCode();

            if (response.IsSuccessStatusCode)
            {
                // 取得response的資料
                string result_str = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                //Console.WriteLine("create pool: " + result_str);
                Pool_create_response result = await response.Content.ReadAsAsync<Pool_create_response>();

                return result;
            }
            else
            {
                return new Pool_create_response { data = null, error = "Failed to fetch API data" };
            }
        }


        public async Task<Pool_excute_response> ExcutePoolAsync(Pool_excute pool_excute, string mode = "", string api_key="")
        {
            //HttpResponseMessage response = await client.PostAsJsonAsync("api/mysql/pool/execute", pool_excute);
            //response.EnsureSuccessStatusCode();
            try
            {
                string path = string.Format("api/mysql/pools/execute");

                // 將 data 轉為 json
                string json = JsonConvert.SerializeObject(pool_excute);
                // 將轉為 string 的 json 依編碼並指定 content type 存為 httpcontent
                HttpContent contentPost = new StringContent(json, Encoding.UTF8, "application/json");
                // 發出 post 並取得結果
                HttpResponseMessage response = await client.PostAsync(path, contentPost).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                Pool_excute_response result;
                if (response.IsSuccessStatusCode)
                {
                    // 將回應結果內容取出並轉為 string 再透過 linqpad 輸出
                    string result_str = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    //Console.WriteLine("execute pool: " + result_str);
                    if (mode == "insert" || mode == "delete" || mode == "update")
                    {
                        dynamic json_obj = JObject.Parse(result_str);
                        JArray jArray = new JArray(json_obj.data);

                        result = new Pool_excute_response
                        {
                            data = jArray,
                            error = json_obj.error
                        };
                    }
                    else
                    {
                        result = await response.Content.ReadAsAsync<Pool_excute_response>();
                    }

                    return result;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }

           

            //string[] table_colums = { "Test Program", "Lot ID", "Wafer Lot", "Tester", "Date", "siteid", "device", "x", "y", "value", "bin_code", "x_Y", "ITEM NUM", "ITEM NAME" };
            //dynamic json_obj = JObject.Parse(result_str);
            ////Console.WriteLine(json_obj.data);
            //Console.WriteLine(json_obj.error);
            //dynamic json_data = JObject.Parse(json_obj.data);
            //Console.WriteLine(json_data.Date);

            //Console.WriteLine(json_obj.data[0][table_colums[0]]);
            //Console.WriteLine(json_obj.data[0]);

            // return URI of the created resource.
            //return response.Headers.Location;

            //return result_str;
        }

        public async Task<Pool_delete_response> DeletePoolAsync(Pool_delete pool_delete, string api_key="")
        {
            string path = string.Format("api/mysql/pools/delete/{0}", pool_delete.pool);

            // 發出 post 並取得結果
            HttpResponseMessage response = await client.DeleteAsync(path).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                // 將回應結果內容取出並轉為 string 再透過 linqpad 輸出
                string result_str = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                //Console.WriteLine("delete pool: " + result_str);
                Pool_delete_response result = await response.Content.ReadAsAsync<Pool_delete_response>();

                return result;
            }
            else
            {
                return null;
            }

            //HttpStatusCode httpStatusCode = response.StatusCode;

            //// return URI of the created resource.
            //return response.Headers.Location;
        }


        public bool checkDBConnect(string pool_name)
        {
            try
            {
                //Signin_response signin_response = getApiKeyValueAsync(pool_signin).GetAwaiter().GetResult();

                // 確認Pool中是否有此 pool_name
                Pool_get_all_response getPoolResponse = GetPoolAsync().GetAwaiter().GetResult();
                //dynamic json_str = JObject.Parse(getPoolResponse.data);
                JArray pool_name_jarray = (JArray)getPoolResponse.data["pool_name"];
                List<string> pool_name_list = pool_name_jarray.ToObject<List<string>>();

                bool contains_in = pool_name_list.Contains(pool_name);

                return contains_in;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }


    }
}
