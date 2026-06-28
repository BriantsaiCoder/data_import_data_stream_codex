using System.Collections.Generic;
using System.Data;
using DCT_data_import;
using Xunit;

namespace DCT_data_import.Tests
{
    public class FileProcessHelperTests
    {
        [Theory]
        [InlineData(null, null, "No Data")]
        [InlineData("", null, "No Data")]
        [InlineData("   ", null, "No Data")]
        [InlineData("   ", "N/A", "N/A")]
        [InlineData("  value  ", null, "value")]
        [InlineData(" 0 ", null, "0")]
        public void ConvertEmptyToDefaultString_NormalizesEmptyAndTrimsValues(
            string input,
            string defaultValue,
            string expected)
        {
            var fileProcess = new FileProcess();

            string actual = defaultValue == null
                ? fileProcess.ConvertEmptyToDefaultString(input)
                : fileProcess.ConvertEmptyToDefaultString(input, defaultValue);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void AddColumnForDataset_WhenRowsExist_AddsColumnsAndRoundsToNinePlaces()
        {
            var dataSet = new DataSet();
            dataSet.Tables.Add(StatisticTableWithRow());
            dataSet.Tables.Add(StatisticTableWithRow());
            var values = new List<StatisticItem>
            {
                new StatisticItem { avg = 1.1234567894m, avg2 = 2.9876543216m, pass_n = 3m },
                new StatisticItem { avg = 4.0000000004m, avg2 = 5.0000000005m, pass_n = 6m }
            };

            new FileProcess().AddColumnForDataset(dataSet, values);

            Assert.Equal(1.123456789m, dataSet.Tables[0].Rows[0]["AVG"]);
            Assert.Equal(2.987654322d, dataSet.Tables[0].Rows[0]["avg_2"]);
            Assert.Equal(3, dataSet.Tables[0].Rows[0]["pass_n"]);
            Assert.Equal(4.000000000m, dataSet.Tables[1].Rows[0]["AVG"]);
            Assert.Equal(5.000000000d, dataSet.Tables[1].Rows[0]["avg_2"]);
            Assert.Equal(6, dataSet.Tables[1].Rows[0]["pass_n"]);
        }

        [Fact]
        public void AddColumnForDataset_WhenTableHasNoRows_StillAddsColumns()
        {
            var dataSet = new DataSet();
            DataTable table = StatisticTable();
            dataSet.Tables.Add(table);

            new FileProcess().AddColumnForDataset(dataSet, new List<StatisticItem>());

            Assert.True(table.Columns.Contains("avg_2"));
            Assert.True(table.Columns.Contains("pass_n"));
            Assert.Empty(table.Rows);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void TryGetRequiredInsertId_WhenInsertIdIsNotPositive_ReturnsFalse(long insertIdValue)
        {
            bool ok = FileProcess.TryGetRequiredInsertId(
                new DbObject.DbCommandResult { InsertId = insertIdValue },
                "lots_info",
                out string insertId,
                out string error);

            Assert.False(ok);
            Assert.Equal(string.Empty, insertId);
            Assert.Contains("lots_info", error);
        }

        [Fact]
        public void TryGetRequiredInsertId_WhenResponseIsNull_ReturnsFalse()
        {
            bool ok = FileProcess.TryGetRequiredInsertId(
                null,
                "lots_info",
                out string insertId,
                out string error);

            Assert.False(ok);
            Assert.Equal(string.Empty, insertId);
            Assert.Contains("lots_info", error);
        }

        [Fact]
        public void TryGetRequiredInsertId_WhenInsertIdIsPositive_ReturnsInvariantString()
        {
            bool ok = FileProcess.TryGetRequiredInsertId(
                new DbObject.DbCommandResult { InsertId = 42 },
                "lots_info",
                out string insertId,
                out string error);

            Assert.True(ok);
            Assert.Equal("42", insertId);
            Assert.Equal(string.Empty, error);
        }

        private static DataTable StatisticTableWithRow()
        {
            DataTable table = StatisticTable();
            table.Rows.Add(0m);
            return table;
        }

        private static DataTable StatisticTable()
        {
            var table = new DataTable();
            table.Columns.Add("AVG", typeof(decimal));
            return table;
        }
    }
}
