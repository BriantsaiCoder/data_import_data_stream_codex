using Microsoft.VisualStudio.TestTools.UnitTesting;
using DCT_data_import;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;

namespace DCT_data_import.Tests
{
    [TestClass()]
    public class FileProcessTests
    {
        [TestMethod()]
        public void FileReadRawDataTest()
        {
            // arrange
            FileProcess fileProcess = new FileProcess();
            var path = @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\MT7658CSN_BZAD-CJG-L_23UYJFB001_2022_06_06_12_07_15.csv";

            // act
            //RawDataContentFormat fileContentFormat = fileProcess.FileReadRawData(path);

            // assert
            //Assert.IsTrue((fileContentFormat.lotInfo != null) && (fileContentFormat.lotStatistic != null) && (fileContentFormat.lotResult != null)
            //    && (fileContentFormat.lotInfo.Rows.Count > 0) && (fileContentFormat.lotStatistic.Tables.Count > 0) && (fileContentFormat.lotResult.Rows.Count > 0));

        }

        [TestMethod()]
        public void FileReadTesterStatusTest()
        {
            // arrange
            FileProcess fileProcess = new FileProcess();
            var path = @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\Tester_Status.csv";

            // act
            //TestStatusContentFormat testStatusContentFormat = fileProcess.FileReadTesterStatus(path);

            // assert
            //Assert.IsTrue((testStatusContentFormat.tester_device_info != null) && (testStatusContentFormat.tester_status != null) && (testStatusContentFormat.tester_sw_version != null) && (testStatusContentFormat.tester_production_analysis != null)
            //    && (testStatusContentFormat.tester_device_info.Rows.Count > 0) && (testStatusContentFormat.tester_status.Rows.Count > 0) && (testStatusContentFormat.tester_sw_version.Rows.Count > 0) && (testStatusContentFormat.tester_production_analysis.Rows.Count > 0));

        }

        [TestMethod()]
        public void FileReadUIStatusTest()
        {
            // arrange
            FileProcess fileProcess = new FileProcess();
            var path = @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\UI_Status.csv";

            // act
            //UIStatusContentFormat uiStatusContentFormat = fileProcess.FileReadUIStatus(path);

            // assert
            //Assert.IsTrue((uiStatusContentFormat.UI_status != null) && (uiStatusContentFormat.UI_status.Rows.Count > 0));

        }

        [TestMethod()]
        public void FileReadFailPinLogTest()
        {
            // arrange
            FileProcess fileProcess = new FileProcess();
            var path = @"D:\ASEKH\K09865\DCT data\每一批產生之檔案\012KA3B001-AT-.csv";

            // act
            //FailPinLogContentFormat failPinLogContent = fileProcess.FileReadFailPinLog(path);

            // assert
            //Assert.IsTrue((failPinLogContent.fail_pin_rate_info != null) && (failPinLogContent.fail_pin_rate_list != null) && (failPinLogContent.fail_pin_rate_list_pin_ball != null)
            //    && (failPinLogContent.fail_pin_rate_info.Rows.Count > 0) && (failPinLogContent.fail_pin_rate_list.Rows.Count > 0) && (failPinLogContent.fail_pin_rate_list_pin_ball.Rows.Count > 0));

        }

        [TestMethod()]
        public void eraseSpecificCharTest()
        {
            // arrange
            FileProcess fileProcess = new FileProcess();

            // act
            string[] values = fileProcess.EraseSpecificChar(",,,,,, Spec MAX, 3, 9999, 9999, 9999, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 9999, 9999, 9999, 3, 9999, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 9999, 9999, 9999, 9999, 9999, 9999, 9999, 9999, 9999,");

            string[] values_tmp_space = values.Where(x => string.IsNullOrEmpty(x)).ToArray();
            string[] values_tmp_comma = values.Where(x => x == ",").ToArray();

            // assert
            Assert.IsFalse(values_tmp_space.Length + values_tmp_comma.Length > 1);
            //Assert.AreEqual(1, 1);
        }

        [TestMethod()]
        public void customizeDateTimeParserTest()
        {
            // arrange
            FileProcess fileProcess = new FileProcess();
            DateTime dateTime;

            // act
            string datetime = fileProcess.CustomizeDateTimeParser("Jun_06_2022_12_08_22");


            // assert
            Assert.IsTrue(DateTime.TryParse(datetime, out dateTime));
        }

        [TestMethod()]
        public void isFileNameExistInDBTest()
        {
            // arrange
            FileProcess fileProcess = new FileProcess();

            // act
            //bool result = fileProcess.isFileNameExistInDB("MT7658CSN_BZAD-CJG-L_23UYJFB001_2022_06_06_12_07_15");


            // assert
            //Assert.IsTrue(result);
        }
    }
}