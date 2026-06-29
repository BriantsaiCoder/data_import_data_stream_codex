using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DCT_data_import;
using DCT_data_import.ReadAndImport;
using Xunit;

namespace DCT_data_import.Tests
{
    [Collection("RuntimeMode")]
    public class ParserCharacterizationTests : IDisposable
    {
        private readonly string _root;

        public ParserCharacterizationTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "dct-parser-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "TSMC_DATA", "LotID"));
            File.WriteAllText(
                Path.Combine(_root, "TSMC_DATA", "LotID", "lot_mapping.csv"),
                JoinLines("tsmc_lot,ase_lot,csv", "LOT123,ASE456,net.csv"));
            ImportSourceSettings.SetOverridesForTests("Local", _root, "Delete");
        }

        public void Dispose()
        {
            ImportSourceSettings.ClearOverridesForTests();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Fact]
        public void FileReadTesterStatus_WhenFourSectionsExist_PopulatesSectionRows()
        {
            TestStatusContentFormat result = new Tester().FileReadTesterStatus(Reader(JoinLines(
                "Device information",
                "DB_Key,Mac_Address,IP_Address",
                "DB001,MAC001,127.0.0.1",
                "Tester status",
                "DPW,Duts,CSV Name",
                "1,2,tester.csv",
                "SW version",
                "PUI version,handler_sw_version",
                "pui-1,handler-1",
                "Production analysis",
                "site1_yield,site2_yield",
                "98,97")));

            Assert.Equal("DB001", result.Tester_device_info.Rows[0][CsvColumnNames.DbKeyUnderscore]);
            Assert.Equal("tester.csv", result.Tester_status.Rows[0]["CSV Name"]);
            Assert.Equal("pui-1", result.Tester_sw_version.Rows[0]["PUI version"]);
            Assert.Equal("98", result.Tester_production_analysis.Rows[0]["site1_yield"]);
        }

        [Fact]
        public void FileReadFailPinLog_WhenOldAndNewFormatsExist_PopulatesSnNumShape()
        {
            FailPinLogContentFormat oldFormat = new FailPin().FileReadFailPinLog(Reader(JoinLines(
                FailPinInfoLines(),
                new[]
                {
                    "DUT,Site,Fail Type",
                    "1,2,Open,P1(B1),;,remark,@,ItemA,1.1,2.2,3.3"
                })));
            FailPinLogContentFormat newFormat = new FailPin().FileReadFailPinLog(Reader(JoinLines(
                FailPinInfoLines(),
                new[]
                {
                    "DUT,SN Num,Site,Fail Type",
                    "1,SN001,2,Open,P2(B2)"
                })));

            Assert.False(oldFormat.HasSnNum);
            Assert.Equal(string.Empty, oldFormat.Fail_pin_rate_list.Rows[0]["sn_num"]);
            Assert.Equal("P1", oldFormat.Fail_pin_rate_list_pin_ball.Rows[0]["pin"]);
            Assert.True(newFormat.HasSnNum);
            Assert.Equal("SN001", newFormat.Fail_pin_rate_list.Rows[0]["sn_num"]);
        }

        [Fact]
        public void FileReadUIStatus_WhenHeaderAndRowExist_PassesCurrentColumnComparison()
        {
            UIStatusContentFormat result = new UiStatus().FileReadUIStatus(Reader(JoinLines(
                "Mac_Address,Area,Factory,OS_Machine,Date",
                "MAC001,A1,F1,M1,2026-01-01")));

            Assert.True(result.CompareUiStatus());
            Assert.Equal("MAC001", result.UI_status.Rows[0]["Mac_Address"]);
        }

        [Fact]
        public void FileReadRecoveryRateData_WhenInfoAndRowsExist_MergesFinalRows()
        {
            RecoveryRateDataContentFormat result = new RecoveryRate().FileReadRecoveryRateData(Reader(JoinLines(
                "DB Key,DB001",
                "Area,A1",
                "Factory,F1",
                "OS Machine,M1",
                "Customer,C1",
                "Program,P1",
                "AO Lot,L1",
                "Mode,Auto",
                "Date,2026-01-01",
                "Test_Item,Defect_mode,reTestPass,FailPinCount,Total_Unit,Recovery rate(%)",
                "Item1,Open,2,1,10,20",
                "Item2,Short,3,2,20,15")));

            Assert.Equal(2, result.FinalRecoveryRateTable.Rows.Count);
            Assert.Equal("DB001", result.FinalRecoveryRateTable.Rows[0][CsvColumnNames.DbKeyWithSpace]);
            Assert.Equal("Item2", result.FinalRecoveryRateTable.Rows[1]["Test_Item"]);
        }

        [Fact]
        public void FileReadRawData_WhenNonTsmcFixtureExists_PopulatesRawResultAndStatisticShape()
        {
            string fixture = RawDataFixture();

            RawDataContentFormat raw = new RawData().FileReadRawData(Reader(fixture));
            RawDataContentFormat multiSpec = new MultiSpecRawData().FileReadRawData(Reader(fixture));

            AssertRawDataShape(raw);
            AssertRawDataShape(multiSpec);
        }

        [Fact]
        public void FileReadIeda_WhenFixedWidthRowsExist_SplitsTitleAndContent()
        {
            IedaDataFormat result = new TsmcIeda().FileReadIeda(Reader(JoinLines(
                FixedWidthTitleLine(),
                FixedWidthContentLine())));

            Assert.Equal("LOT123", result.IedaTitle.Rows[0]["lot_id"]);
            Assert.Equal("ASE456", result.IedaTitle.Rows[0]["ase_lot"]);
            Assert.Equal("TD000001", result.IedaContent.Rows[0]["touch_down"]);
            Assert.Equal("SN000001", result.IedaContent.Rows[0]["serial_number"]);
        }

        [Fact]
        public void FileReadIeda_WhenFileSourceInjected_LoadsLotMappingFromThatSource()
        {
            ImportSourceSettings.ClearOverridesForTests();

            IedaDataFormat result = new TsmcIeda(new LocalImportFileSource(_root, LocalSuccessAction.Delete))
                .FileReadIeda(Reader(FixedWidthTitleLine()));

            Assert.Equal("LOT123", result.IedaTitle.Rows[0]["lot_id"]);
            Assert.Equal("ASE456", result.IedaTitle.Rows[0]["ase_lot"]);
        }

        private static void AssertRawDataShape(RawDataContentFormat result)
        {
            Assert.Equal("DB001", result.LotInfo.Rows[0][CsvColumnNames.DbKeyUnderscore]);
            Assert.Equal("1", result.LotResult.Rows[0]["Serial"]);
            Assert.Equal("N", result.LotResult.Rows[0]["retest_loc"]);
            Assert.Equal(2, result.LotStatistic.Tables.Count);
            Assert.Equal("1001", result.LotStatistic.Tables[0].Rows[0]["Item No"]);
            Assert.Equal("[1]", result.LotStatistic.Tables[0].Rows[0]["value"]);
            Assert.Equal("uA", result.LotStatistic.Tables[1].Rows[0]["unit"]);
            Assert.Equal("[2]", result.LotStatistic.Tables[1].Rows[0]["value"]);
        }

        private static string RawDataFixture()
        {
            return JoinLines(
                "Version:,1.0",
                "Mac_Address:,MAC001",
                "DB_Key:,DB001",
                "Customer:,ASE",
                "Package:,PKG",
                "BondingDiagram:,BD",
                "Program:,PGM",
                "Device:,DEV",
                "Control_lot:,CTRL",
                "AO_lot:,AOL",
                "OS_Machine_ID:,OSM",
                "Stop:,2026-01-01 01:00:00",
                ",,,,,,,1001,1002",
                ",,,,,,,Leak,Short",
                "Force,1,2",
                "Serial,SN Num,SiteID,X,Y,HBIN,P/F,V,uA,test time,index time,real time",
                "1,SN001,1,10,20,5,P,1.,2(S),0.5,0.1,0.6");
        }

        private static string FixedWidthTitleLine()
        {
            string[] values =
            {
                "LOT123", "A1", "DEVICE-A", "MPW1", "PRD001", "TESTER1", "OPER1", "PROGRAM-A",
                "2026-01-01 01:00:00", "2026-01-01 02:00:00", "SOCKET", "LOADBD", "BD",
                "N", "1", "SITE01", "FD", "COVER", "SOCKETID", "HANDLER", "REV1", "TSMCLOT",
                "2026-01-01", "2026-01-02"
            };

            return FixedWidth(new IedaDataFormat().titleColumnsDataSize.Skip(1), values);
        }

        private static string FixedWidthContentLine()
        {
            string[] values =
            {
                "TD000001", "SB01", "PASS", "0001", "00000001", "00000002", "N", "ARM1",
                "25C", "2026-01-01 01:30:00", "10N", "WAFER01", "001", "002", "SN000001",
                "EFUSE1", "EFUSE2", "EFUSE3", "EFUSE4", "SP1", "SP2", "SP3", "SP4",
                "SOFTBIN", "0001", "HARDBIN", "QR001"
            };

            return FixedWidth(new IedaDataFormat().contentColumnsDataSize.Skip(1), values);
        }

        private static string FixedWidth(IEnumerable<int> widths, IReadOnlyList<string> values)
        {
            return string.Concat(widths.Zip(values, (width, value) => value.PadRight(width).Substring(0, width)));
        }

        private static string[] FailPinInfoLines()
        {
            return new[]
            {
                "Mac Address,MAC001",
                "DB Key,DB001",
                "Area,A1",
                "Factory,F1",
                "OS Machine,M1",
                "AO Lot,L1",
                "Mode,Auto",
                "Data format,Pin",
                "File Name,fail_pin_DB001.csv",
                "Date,2026-01-01",
                "Total,1",
                "Pass,0",
                "Open,1",
                "Short,0",
                "LK,0",
                "nVTEP,0"
            };
        }

        private static StreamReader Reader(string text)
        {
            return new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(text)));
        }

        private static string JoinLines(params string[] lines)
        {
            return string.Join("\r\n", lines);
        }

        private static string JoinLines(params IEnumerable<string>[] lineGroups)
        {
            return string.Join("\r\n", lineGroups.SelectMany(x => x));
        }
    }
}
