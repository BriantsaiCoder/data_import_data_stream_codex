using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DCT_data_import.ApiObject;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Threading;

namespace DCT_data_import
{
    class Program
    {
        //public static string POOL_NAME = "DB_program";
        //public static string HOST = "192.168.0.105";
        //public static string PORT = "3308";
        //public static string USER = "5910";
        //public static string PASSWORD = "5910";
        //public static string DATABASE = "dct_test";

        //public static string FTP_IP = "10.16.92.65";
        //public static string FTP_USER = "tid5910";
        //public static string FTP_PASSWORD = "5910@tid";


        public static string POOL_NAME = "C_sharp_dct_import";
        public static string HOST = "192.168.0.101";
        public static string PORT = "3306";
        public static string USER = "5940";
        public static string PASSWORD = "5940";
        public static string DATABASE = "dct";


        public static string FTP_IP = "10.16.92.67";
        public static string FTP_USER = "jacky";
        public static string FTP_PASSWORD = "jacky";

        static void Main(string[] args)
        {
            //double num1 = 0.123456789;
            //double num2 = num1 * num1;
            //double num3 = num2 * num2;
            //double num4 = num3 * num3;

            //Console.WriteLine("num1="+ num1);
            //Console.WriteLine("num2=" + num2);
            //Console.WriteLine("num3=" + num3);
            //Console.WriteLine("num4=" + num4);

            FileProcess fileAccess = new FileProcess();
            WebApiClient webApiClient = new WebApiClient();

            bool isConnect = webApiClient.checkDBConnect(POOL_NAME);

            if (!isConnect)
            {
                Pool pool = new Pool
                {
                    pool_name = POOL_NAME,
                    host = HOST,
                    port = PORT,
                    user = USER,
                    password = PASSWORD,
                    database = DATABASE
                };
                try
                {
                    var create_response = webApiClient.CreatePoolAsync(pool).GetAwaiter().GetResult();
                    if (create_response.error != null)
                    {
                        throw new Exception("Pool 建立失敗: " + create_response.error);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    webApiClient.client.Dispose();
                }
            }


            //String ftpserver = "ftp://10.16.92.67/Data_Analysis/Data_Cloud_CSV/";
            //FtpWebRequest reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            //reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
            //reqFTP.Credentials = new NetworkCredential("jacky", "jacky");
            //FtpWebResponse response = (FtpWebResponse)reqFTP.GetResponse();

            Thread thread1 = new Thread(() => readAndImportRawData(fileAccess, webApiClient));
            Thread thread2 = new Thread(() => readAndImportTesterStatus(fileAccess, webApiClient));
            Thread thread3 = new Thread(() => readAndImportUIStatus(fileAccess, webApiClient));
            Thread thread4 = new Thread(() => readAndImportFailPinLog(fileAccess, webApiClient));

            thread1.Start();
            thread2.Start();
            thread3.Start();
            thread4.Start();

            //readAndImportRawData(fileAccess, webApiClient);
            //readAndImportTesterStatus(fileAccess, webApiClient);
            //readAndImportUIStatus(fileAccess, webApiClient);
            //readAndImportFailPinLog(fileAccess, webApiClient);

            //bool IfRunOver = false;
            //while (!IfRunOver)
            //{
            //    bool IfTimesEnd = thread1.IsAlive;

            //    if (!IfTimesEnd || IfRunOver)
            //    {
            //        thread1.Interrupt();
            //        thread1.Abort();
            //        IfTimesEnd = false;
            //        break;
            //    }
            //}

            //callWebApi();
            //ftpReadFiles();
            Console.WriteLine("The End~");
            //Console.Read();
        }
        

        static void callWebApi()
        {
            WebApiClient webApiClient = new WebApiClient();

            // 建立新連線
            Pool pool = new Pool
            {
                pool_name = POOL_NAME,
                host = HOST,
                port = PORT,
                user = USER,
                password = PASSWORD,
                database = DATABASE
            };
            try
            {
                var response = webApiClient.CreatePoolAsync(pool).GetAwaiter().GetResult();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return;
            }

            var get_str = webApiClient.GetPoolAsync().GetAwaiter().GetResult();


            Pool_excute pool_excute = new Pool_excute
            {
                pool = POOL_NAME,
                query = "SELECT 1+1;"
            };
            var response2 = webApiClient.ExcutePoolAsync(pool_excute).GetAwaiter().GetResult();


            Pool_delete pool_delete = new Pool_delete
            {
                pool = POOL_NAME
            };
            var response3 = webApiClient.DeletePoolAsync(pool_delete).GetAwaiter().GetResult();


            Console.ReadLine();
        }
        
        static void ftpReadFiles()
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            string names;
            List<string> list_filename;

            ftpserver = "ftp://10.16.92.65/Data_Analysis/Data_Cloud_CSV/";
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFTP.Credentials = new NetworkCredential("tid5910", "5910@tid");
            response = (FtpWebResponse)reqFTP.GetResponse();

            responseStream = response.GetResponseStream();
            reader = new StreamReader(responseStream);

            names = reader.ReadToEnd();
            list_filename = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            for (int i = 0; i < list_filename.Count; i++)
            {
                ftpserver = "ftp://10.16.92.65/Data_Analysis/Data_Cloud_CSV/" + list_filename[i];
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
                response = (FtpWebResponse)reqFTP.GetResponse();
                responseStream = response.GetResponseStream();
                reader = new StreamReader(responseStream);
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    Console.WriteLine(line);
                }
            }
        }

        static void readAndImportRawData(FileProcess fileAccess, WebApiClient webApiClient)
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            string names;
            List<string> list_filename;
            string[] fileNameSplit;
            WriteToLog writeToLog = new WriteToLog();
            CompareTool compareTool = new CompareTool();
            bool compare_result = false;
            CalculateSPC calculateSPC = new CalculateSPC();
            List<StatisticItem> avg_2;
            string downloadStatus, deleteStatus;


            ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/";
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
            response = (FtpWebResponse)reqFTP.GetResponse();

            responseStream = response.GetResponseStream();
            reader = new StreamReader(responseStream);

            names = reader.ReadToEnd();
            list_filename = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            for (int i = list_filename.Count - 1; i >= 0; i--)
            { 
                try
                {
                    fileNameSplit = list_filename[i].Split('.');
                    if (fileNameSplit[fileNameSplit.Length - 1] != "csv") continue;
                    bool fileExist = fileAccess.isFileNameExistInDB(fileNameSplit[0], webApiClient);
                    bool import_result = false;
                    bool isDBKeyExist = false;
                    
                    ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV/" + list_filename[i];
                    reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                    reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
                    response = (FtpWebResponse)reqFTP.GetResponse();
                    responseStream = response.GetResponseStream();
                    reader = new StreamReader(responseStream);

                    if (!fileExist)
                    {
                        RawDataContentFormat rawDataContentFormat = fileAccess.FileReadRawData(reader);
                        reader.Close();
                        if (rawDataContentFormat == null || rawDataContentFormat.lotInfo.Rows.Count < 1)
                        {
                            Console.WriteLine("Raw data 讀檔失敗:  " + list_filename[i]);
                            writeToLog.writeToLog("Raw data  讀檔失敗: " + ftpserver);
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            //RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV_/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            continue;
                        }
                        if (!rawDataContentFormat.compareInfo())
                        {
                            Console.WriteLine("Raw data 之 information 欄位名稱不符:  " + list_filename[i]);
                            writeToLog.writeToLog("Raw data 之 information 欄位名稱不符:" + ftpserver);
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            //RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV_/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            return;
                        }
                        if (!rawDataContentFormat.compareStatistic())
                        {
                            Console.WriteLine("Raw data 之 statistic 欄位名稱不符:  " + list_filename[i]);
                            writeToLog.writeToLog("Raw data 之 statistic 欄位名稱不符:" + ftpserver);
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            //RenameFile(ftpserver, "/Data_Analysis/Data_Cloud_CSV_/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            return;
                        }
                        //fileAccess.caculatePpk(rawDataContentFormat.lotStatistic);
                        //if (rawDataContentFormat.lotResult.Rows.Count < 1)
                        //{
                        //    Console.WriteLine("Lot Result 無資料");
                        //}

                        isDBKeyExist = fileAccess.isDBKeyExistInDB("lots_info", rawDataContentFormat.lotInfo.Rows[0]["DB_Key"].ToString(), webApiClient);
                        if (isDBKeyExist)
                        {
                            compare_result = compareTool.compareRawData(rawDataContentFormat, webApiClient);
                            Console.WriteLine("資料庫已存在此資料: Raw data 比對: " + compare_result + "   檔名:" + list_filename[i]);
                            ////writeToLog.writeToLog("資料庫已存在此資料:" + ftpserver);
                            //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);

                            // 下載檔案到本地端
                            downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\raw_data_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);

                            // 刪除已存在的的CSV檔案
                            deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                        }
                        else
                        {
                            //// 計算平方和
                            //list_square_sum = calculateSPC.SquareSum(rawDataContentFormat);
                            // 計算均方和
                            avg_2 = calculateSPC.AverageOfSumSquare(rawDataContentFormat);
                            fileAccess.addColumnForDataset(rawDataContentFormat.lotStatistic, "avg_2", avg_2);

                            //if(fileNameSplit[0]=="65109QH7XV01_46SMZMB002_2022_11_17_14_00_50")
                            //{
                            //    Console.WriteLine("");
                            //}
                            
                            // 開始匯入
                            import_result = fileAccess.importRawData(rawDataContentFormat, webApiClient);

                            compare_result = compareTool.compareRawData(rawDataContentFormat, webApiClient);

                            if (import_result)
                            {
                                Console.WriteLine("匯入完成! Raw data    比對: " + compare_result + "   檔名:" + list_filename[i]);

                                // 下載檔案到本地端
                                downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\raw_data_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                                // 刪除已存在的的CSV檔案
                                deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                            }
                            else
                            {
                                Console.WriteLine("匯入失敗: Raw data " + list_filename[i]);
                                writeToLog.writeToLog("匯入失敗:" + ftpserver);
                                RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            }
                        }

                        //reader.Close();
                        response.Close();
                    }
                    else
                    {
                        Console.WriteLine("資料庫已存在此檔名: Raw data " + list_filename[i]);
                        writeToLog.writeToLog("資料庫已存在此檔名:" + ftpserver);
                        reader.Close();
                        response.Close();

                        // 下載檔案到本地端
                        downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\raw_data_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        // 刪除已存在的的CSV檔案
                        deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);

                        //RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);


                    }
                    //string[] allLines = reader.ReadToEnd().Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                }
                catch (Exception ex)
                {
                    //Console.WriteLine(ftpserver + ": " + ex.ToString());
                    writeToLog.writeToLog(ftpserver + ": " + ex.ToString());
                    RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Data_Cloud_CSV_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                }
                Thread.Sleep(500);
            }

            reader.Close();
            response.Close();
            Console.WriteLine("Raw data end~");
        }

        static void readAndImportTesterStatus(FileProcess fileAccess, WebApiClient webApiClient)
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            string names;
            List<string> list_filename;
            string[] fileNameSplit;
            bool isDBKeyExist = false, import_result = false;
            WriteToLog writeToLog = new WriteToLog();
            CompareTool compareTool = new CompareTool();
            bool compare_result = false;
            string downloadStatus, deleteStatus;

            // Tester Status
            ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Tester_Status/";
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
            response = (FtpWebResponse)reqFTP.GetResponse();

            responseStream = response.GetResponseStream();
            reader = new StreamReader(responseStream);

            names = reader.ReadToEnd();
            list_filename = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();


            for (int i = list_filename.Count - 1; i >= 0; i--)
            {
                try
                {
                    fileNameSplit = list_filename[i].Split('.');
                    if (fileNameSplit[fileNameSplit.Length - 1] != "csv") continue;
                    

                    ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/Tester_Status/" + list_filename[i];
                    reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                    reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
                    //ftpserver = "ftp://10.16.92.67/Data_Analysis/Tester_Status/" + list_filename[i];
                    //reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                    //reqFTP.Credentials = new NetworkCredential("jacky", "jacky");
                    response = (FtpWebResponse)reqFTP.GetResponse();
                    responseStream = response.GetResponseStream();
                    reader = new StreamReader(responseStream);

                    TestStatusContentFormat testStatusContentFormat = fileAccess.FileReadTesterStatus(reader);
                    reader.Close();
                    if (testStatusContentFormat == null || testStatusContentFormat.tester_device_info.Rows.Count < 1)
                    {
                        Console.WriteLine("Tester Status 讀檔失敗:  " + list_filename[i]);
                        writeToLog.writeToLog("Tester Status  讀檔失敗: " + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        continue;
                    }
                    if (!testStatusContentFormat.compareInfo())
                    {
                        Console.WriteLine("Tester Status 之 information 欄位名稱不符:  " + list_filename[i]);
                        writeToLog.writeToLog("Tester Status 之 information 欄位名稱不符: " + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        Console.Read();
                        return;
                    }
                    if (!testStatusContentFormat.compareStatus())
                    {
                        Console.WriteLine("Tester Status 之 tester_status 欄位名稱不符:  " + list_filename[i]);
                        writeToLog.writeToLog("Tester Status 之 tester_status 欄位名稱不符: " + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        Console.Read();
                        return;
                    }
                    isDBKeyExist = fileAccess.isDBKeyExistInDB("tester_device_info", testStatusContentFormat.tester_device_info.Rows[0]["DB_Key"].ToString(), webApiClient);
                    if (isDBKeyExist)
                    {
                        compare_result = compareTool.compareTesterStatus(testStatusContentFormat, webApiClient);
                        Console.WriteLine("資料庫已存在此資料: Tester Status  比對" + compare_result + "   檔名:" + list_filename[i]);
                        writeToLog.writeToLog("資料庫已存在此資料: " + ftpserver);

                        // 下載檔案到本地端
                        downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\tester_status_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        // 刪除已存在的的CSV檔案
                        deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                    }
                    else
                    {
                        import_result = fileAccess.importTesterStatus(testStatusContentFormat, webApiClient);
                        compare_result = compareTool.compareTesterStatus(testStatusContentFormat, webApiClient);
                        if (import_result)
                        {
                            Console.WriteLine("匯入完成! Tester Status  比對" + compare_result + "   檔名: " + list_filename[i]);
                            // 下載檔案到本地端
                            downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\tester_status_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            // 刪除已存在的的CSV檔案
                            deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                        }
                        else
                        {
                            Console.WriteLine("匯入失敗: Tester Status " + list_filename[i]);
                            writeToLog.writeToLog("匯入失敗: " + ftpserver);
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        }
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Tester_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                }

                reader.Close();
                response.Close();

                Thread.Sleep(500);
            }

            Console.WriteLine("Tester status end~");
        }

        static void readAndImportUIStatus(FileProcess fileAccess, WebApiClient webApiClient)
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            string names;
            List<string> list_filename;
            string[] fileNameSplit;

            bool isDBKeyExist = false, import_result = false;
            CompareTool compareTool = new CompareTool();
            WriteToLog writeToLog = new WriteToLog();
            string downloadStatus, deleteStatus;

            // Tester Status
            ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/UI_Status/";
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
            response = (FtpWebResponse)reqFTP.GetResponse();

            responseStream = response.GetResponseStream();
            reader = new StreamReader(responseStream);

            names = reader.ReadToEnd();
            list_filename = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            for (int i = list_filename.Count - 1; i >= 0; i--)
            {
                try
                {
                    fileNameSplit = list_filename[i].Split('.');
                    if (fileNameSplit[fileNameSplit.Length - 1] != "csv") continue;

                     import_result = false;

                    ftpserver = "ftp://" + FTP_IP + "/DCT_Log/DCT_DB_DATA/UI_Status/" + list_filename[i];
                    reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                    reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
                    response = (FtpWebResponse)reqFTP.GetResponse();
                    responseStream = response.GetResponseStream();
                    reader = new StreamReader(responseStream);

                    UIStatusContentFormat uiStatusContentFormat = fileAccess.FileReadUIStatus(reader);
                    reader.Close();
                    if (uiStatusContentFormat == null)
                    {
                        Console.WriteLine("UI Status 讀取失敗: " + list_filename[i]);
                        writeToLog.writeToLog("UI Status 讀取失敗:" + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/UI_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        continue;
                    }
                    if (!uiStatusContentFormat.compareUiStatus())
                    {
                        Console.WriteLine("UI Status 之 ui_status 欄位名稱不符: " + list_filename[i]);
                        writeToLog.writeToLog("UI Status 之 ui_status 欄位名稱不符:" + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/UI_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        Console.Read();
                        return;
                    }

                    import_result = fileAccess.importUIStatus(uiStatusContentFormat, webApiClient);
                    if (import_result)
                    {
                        Console.WriteLine("匯入完成! UI Status " + list_filename[i]);
                        // 下載檔案到本地端
                        downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\ui_status_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        // 刪除已存在的的CSV檔案
                        deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                    }
                    else
                    {
                        Console.WriteLine("匯入失敗: UI Status " + list_filename[i]);
                        writeToLog.writeToLog("匯入失敗:" + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/UI_Status_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                    }

                    Thread.Sleep(500); reader.Close();
                    response.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            Console.WriteLine("UI status end~");
        }

        static void readAndImportFailPinLog(FileProcess fileAccess, WebApiClient webApiClient)
        {
            String ftpserver;
            FtpWebRequest reqFTP;
            FtpWebResponse response;
            Stream responseStream;
            StreamReader reader;
            string names;
            List<string> list_filename;
            string[] fileNameSplit;
            bool import_result = false, isDBKeyExist = false;
            WriteToLog writeToLog = new WriteToLog();
            CompareTool compareTool = new CompareTool();
            bool compare_result = false;
            string downloadStatus, deleteStatus;

            // Tester Status
            ftpserver = "ftp://" + FTP_IP + "/Data_Analysis/Fail_Pin_Log/ST_RT_AT/CSV_Kerwin/";
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
            reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
            response = (FtpWebResponse)reqFTP.GetResponse();

            responseStream = response.GetResponseStream();
            reader = new StreamReader(responseStream);

            names = reader.ReadToEnd();
            list_filename = names.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            for (int i = list_filename.Count - 1; i >= 0; i--)
            {
                try
                {
                    fileNameSplit = list_filename[i].Split('.');
                    if (fileNameSplit[fileNameSplit.Length - 1] != "csv") continue;

                    ftpserver = "ftp://" + FTP_IP + "/Data_Analysis/Fail_Pin_Log/ST_RT_AT/CSV_Kerwin/" + list_filename[i];
                    reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpserver));
                    reqFTP.Credentials = new NetworkCredential(FTP_USER, FTP_PASSWORD);
                    response = (FtpWebResponse)reqFTP.GetResponse();
                    responseStream = response.GetResponseStream();
                    reader = new StreamReader(responseStream);

                    FailPinLogContentFormat failPinLogContent = fileAccess.FileReadFailPinLog(reader);
                    // 讀取失敗或沒有資料
                    if (failPinLogContent == null || failPinLogContent.fail_pin_rate_info.Rows.Count < 1)
                    {
                        Console.WriteLine("Fail Pin Log 讀取失敗:  " + list_filename[i]);
                        writeToLog.writeToLog("Fail Pin Log 讀取失敗: " + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        continue;
                    }
                    reader.Close();
                    if (!failPinLogContent.compareInfo())
                    {
                        Console.WriteLine("Fail Pin Log 之 information 欄位名稱不符:  " + list_filename[i]);
                        writeToLog.writeToLog("Fail Pin Log 之 information 欄位名稱不符: " + ftpserver);
                        RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        return;
                    }
                    isDBKeyExist = fileAccess.isDBKeyExistInDB("fail_pin_rate_info", failPinLogContent.fail_pin_rate_info.Rows[0]["DB Key"].ToString(), webApiClient);
                    if (isDBKeyExist)
                    {
                        compare_result = compareTool.compareFailPinLog(failPinLogContent, webApiClient);
                        Console.WriteLine("資料庫已存在此資料: Fail Pin  " + " 比對: " + compare_result + "   檔名:" + list_filename[i]);
                        //writeToLog.writeToLog("資料庫已存在此資料: " + ftpserver);
                        // 下載檔案到本地端
                        downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\fail_pin_log_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        // 刪除已存在的的CSV檔案
                        deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                    }
                    else
                    {
                        import_result = fileAccess.importFailPinLog(failPinLogContent, webApiClient);
                        compare_result = compareTool.compareFailPinLog(failPinLogContent, webApiClient);
                        if (import_result)
                        {
                            Console.WriteLine("匯入完成! Fail Pin    比對: " + compare_result + "   檔名:" + list_filename[i]);
                            // 下載檔案到本地端
                            downloadStatus = DownloadFile(ftpserver, @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\fail_pin_log_temp\" + list_filename[i], FTP_USER, FTP_PASSWORD);
                            // 刪除完成的CSV檔案
                            deleteStatus = DeleteFile(ftpserver, FTP_USER, FTP_PASSWORD);
                        }
                        else
                        {
                            Console.WriteLine("匯入失敗: Fail Pin " + list_filename[i]);
                            writeToLog.writeToLog("匯入失敗:" + ftpserver);
                            RenameFile(ftpserver, "/DCT_Log/DCT_DB_DATA/Fail_Pin_Log_Error/" + list_filename[i], FTP_USER, FTP_PASSWORD);
                        }
                    }

                    Thread.Sleep(500);
                    reader.Close();
                    response.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    writeToLog.writeToLog(ex.ToString());
                }
            }

            Console.WriteLine("Fail pin log end~");
        }

        static string DeleteFile(string fileName, string user, string password)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fileName);
            request.Method = WebRequestMethods.Ftp.DeleteFile;
            request.Credentials = new NetworkCredential(user, password);

            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                return response.StatusDescription;
            }
        }

        static string RenameFile(string fileName, string newFileName, string user, string password)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fileName);
            request.Method = WebRequestMethods.Ftp.Rename;
            request.Credentials = new NetworkCredential(user, password);
            request.RenameTo = newFileName;

            try
            {
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                
                response.Close();
                return response.StatusDescription;
            }
            catch(Exception ex)
            {
                return null;
            }

        }

        static string DownloadFile(string fileName, string newFileName, string user, string password)
        {
            try
            {
                FileStream outputStream = new FileStream(newFileName, FileMode.Create);


                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fileName);
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.Credentials = new NetworkCredential(user, password);

                FtpWebResponse response = (FtpWebResponse)request.GetResponse();

                //建立物件接收從FTP回應的資料流
                Stream responseStream = response.GetResponseStream();
                //建立物件讀取資料流的字元
                StreamReader reader = new StreamReader(responseStream);

                long cl = response.ContentLength;

                int bufferSize = 2048;

                int readCount;

                byte[] buffer = new byte[bufferSize];

                readCount = responseStream.Read(buffer, 0, bufferSize);

                while (readCount > 0)
                {
                    outputStream.Write(buffer, 0, readCount);

                    readCount = responseStream.Read(buffer, 0, bufferSize);
                }
                //Console.WriteLine(reader.ReadToEnd());

                response.Close();
                responseStream.Close();
                outputStream.Close();
                response.Close();
                return response.StatusDescription;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

    }
}
