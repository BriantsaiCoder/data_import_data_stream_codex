using System;
using System.Threading.Tasks;
using static DCT_data_import.ApiObject;
namespace DCT_data_import
{
    public class WebApiClient
    {
        public WebApiClient()
        {
        }
        public async Task<Pool_execute_response> ExecutePoolAsync(Pool_execute pool_execute, string mode = "select")
        {
            string server = Program.HOST;
            string user = Program.USER;
            string password = Program.PASSWORD;
            string port = Program.PORT;
            string database = Program.DATABASE;
            Pool_execute_response result = new Pool_execute_response();
            try
            {
                DBmysql DB = new DBmysql();
                DB.Connect(server, port, user, password, database);
                string cmd = pool_execute.Query;
                result = DB.Excute_mysql_cmd(cmd, mode);
                return result;
            }
            catch (Exception ex)
            {
                return new Pool_execute_response { Error = ex.ToString() };
            }
        }
    }
}