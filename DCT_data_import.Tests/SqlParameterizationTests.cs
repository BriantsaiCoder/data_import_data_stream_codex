using System;
using System.Data;
using System.Linq;
using Dapper;
using DCT_data_import.ReadAndImport;
using Xunit;
using static DCT_data_import.DbObject;

namespace DCT_data_import.Tests
{
    public class SqlParameterizationTests
    {
        private const string Payload = "'; DROP TABLE db_key;--";

        [Fact]
        public void DbSqlRequest_WhenConstructed_HoldsSqlTextAndParameters()
        {
            var parameters = new { dbKey = "DB001" };

            var request = new DbSqlRequest
            {
                Query = "SELECT id FROM db_key WHERE db_key=@dbKey",
                Parameters = parameters
            };

            Assert.Equal("SELECT id FROM db_key WHERE db_key=@dbKey", request.Query);
            Assert.Same(parameters, request.Parameters);
        }

        [Fact]
        public void BuildDbKeyStatusSelectQuery_KeepsDbKeyOutOfSqlText()
        {
            DbSqlRequest query = DbAccess.BuildDbKeyStatusSelectQuery("db_key", Payload);

            Assert.Contains("@dbKey", query.Query);
            Assert.DoesNotContain(Payload, query.Query);
            Assert.Equal(Payload, AnonymousParameterValue(query.Parameters, "dbKey"));
        }

        [Fact]
        public void BuildDbKeyImportStatusUpdateQuery_KeepsRemarkAndDbKeyOutOfSqlText()
        {
            DbSqlRequest query = DbAccess.BuildDbKeyImportStatusUpdateQuery(Payload, 1, 1, 1, 1, Payload, "1", "0");

            Assert.Contains("@remark", query.Query);
            Assert.Contains("@dbKey", query.Query);
            Assert.DoesNotContain(Payload, query.Query);
            Assert.Equal(Payload, AnonymousParameterValue(query.Parameters, "remark"));
            Assert.Equal(Payload, AnonymousParameterValue(query.Parameters, "dbKey"));
        }

        [Theory]
        [InlineData("tester", "`db_key`")]
        [InlineData("ui_status", "`db_key_ui_status`")]
        public void BuildDataCountInDaysQuery_UsesThresholdParameter(string mode, string expectedTable)
        {
            const long threshold = 987654321;

            DbSqlRequest query = DbAccess.BuildDataCountInDaysQuery(mode, threshold);

            Assert.Contains(expectedTable, query.Query);
            Assert.Contains("@threshold", query.Query);
            Assert.DoesNotContain(threshold.ToString(), query.Query);
            Assert.Equal(threshold, AnonymousParameterValue(query.Parameters, "threshold"));
        }

        [Fact]
        public void BuildDataCountInDaysQuery_RejectsUnsupportedMode()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
                DbAccess.BuildDataCountInDaysQuery("unknown", 987654321));

            Assert.Equal("mode", ex.ParamName);
        }

        [Fact]
        public void BuildTableExistsQuery_KeepsTableNameOutOfSqlText()
        {
            DbSqlRequest query = DatabaseService.BuildTableExistsQuery("dct", Payload);

            Assert.Contains("@tableName", query.Query);
            Assert.DoesNotContain(Payload, query.Query);
            Assert.Equal(Payload, AnonymousParameterValue(query.Parameters, "tableName"));
        }

        [Fact]
        public void BuildInsertQuery_RejectsInvalidTableName()
        {
            var parameters = new DynamicParameters();
            parameters.Add("p0", "value");

            Assert.Throws<ArgumentException>(() =>
                FileProcess.BuildInsertQuery("ieda_title; DROP TABLE db_key;--", "`lot_id`", "@p0", parameters));
        }

        [Fact]
        public void BuildInsertQuery_RejectsInvalidColumnList()
        {
            var parameters = new DynamicParameters();
            parameters.Add("p0", "value");

            Assert.Throws<ArgumentException>(() =>
                FileProcess.BuildInsertQuery("ieda_title", "`lot_id`; DROP TABLE db_key;--", "@p0", parameters));
        }

        [Fact]
        public void AddInsertParameter_KeepsCsvPayloadOutOfMultiRowSql()
        {
            const string csvPayload = "A\"),('B'); DROP TABLE lots_result;--";
            var parameters = new DynamicParameters();
            int parameterIndex = 0;

            string values = FileProcess.AddInsertParameter(parameters, ref parameterIndex, "batch", csvPayload) +
                            "),(" +
                            FileProcess.AddInsertParameter(parameters, ref parameterIndex, "batch", "safe");
            DbSqlRequest query = FileProcess.BuildInsertQuery("lots_result", "`lot_id`", values, parameters);

            Assert.Contains("@batch_0),(@batch_1", query.Query);
            Assert.DoesNotContain(csvPayload, query.Query);
            Assert.True(DynamicParametersContain((DynamicParameters)query.Parameters, csvPayload));
        }

        [Fact]
        public void BuildDeleteRawDataQuery_UsesLotIdParameter()
        {
            DbSqlRequest query = FileProcess.BuildDeleteRawDataQuery(Payload);

            Assert.Contains("@lotId", query.Query);
            Assert.DoesNotContain(Payload, query.Query);
            Assert.Equal(Payload, AnonymousParameterValue(query.Parameters, "lotId"));
        }

        [Fact]
        public void BuildDeleteTesterStatusQuery_UsesDeviceInfoIdParameter()
        {
            DbSqlRequest query = FileProcess.BuildDeleteTesterStatusQuery(Payload);

            Assert.Contains("@deviceInfoId", query.Query);
            Assert.DoesNotContain(Payload, query.Query);
            Assert.Equal(Payload, AnonymousParameterValue(query.Parameters, "deviceInfoId"));
        }

        [Fact]
        public void BuildDeleteFailPinLogQueries_UseFailPinIdParameter()
        {
            DbSqlRequest[] queries = FileProcess.BuildDeleteFailPinLogQueries(Payload);

            Assert.Equal(3, queries.Length);
            foreach (DbSqlRequest query in queries)
            {
                Assert.Contains("@failPinId", query.Query);
                Assert.DoesNotContain(Payload, query.Query);
                Assert.Equal(Payload, AnonymousParameterValue(query.Parameters, "failPinId"));
            }
        }

        [Fact]
        public void BuildIedaTitleInsertQuery_KeepsFileValuesOutOfSqlText()
        {
            var content = new IedaDataFormat();
            DataRow row = content.IedaTitle.NewRow();
            row["lot_id"] = Payload;
            content.IedaTitle.Rows.Add(row);

            DbSqlRequest query = TsmcIeda.BuildIedaTitleInsertQuery(content.IedaTitle, new FileProcess());

            Assert.Contains("@title_", query.Query);
            Assert.DoesNotContain(Payload, query.Query);
            Assert.True(DynamicParametersContain((DynamicParameters)query.Parameters, Payload));
        }

        [Fact]
        public void BuildIedaContentInsertQuery_KeepsFileValuesOutOfSqlText_AndBuildsMultiRowPlaceholders()
        {
            const string titleId = "title-123";
            var content = new IedaDataFormat();
            int serialNumberOrdinal = content.IedaContent.Columns.IndexOf("serial_number");
            DataRow firstRow = content.IedaContent.NewRow();
            firstRow["serial_number"] = Payload;
            content.IedaContent.Rows.Add(firstRow);
            DataRow secondRow = content.IedaContent.NewRow();
            secondRow["serial_number"] = "safe";
            content.IedaContent.Rows.Add(secondRow);

            DbSqlRequest query = TsmcIeda.BuildIedaContentInsertQuery(content.IedaContent, titleId, new FileProcess());
            var parameters = (DynamicParameters)query.Parameters;

            Assert.True(serialNumberOrdinal > 0);
            Assert.Contains("@content_0_0", query.Query);
            Assert.Contains("),(@content_1_", query.Query);
            Assert.DoesNotContain(Payload, query.Query);
            Assert.Equal(titleId, parameters.Get<string>("content_0_0"));
            Assert.Equal(Payload, parameters.Get<string>("content_0_" + serialNumberOrdinal));
            Assert.Equal("safe", parameters.Get<string>("content_1_" + serialNumberOrdinal));
        }

        [Fact]
        public void EscapeDataTableFilterValue_DoublesSingleQuotes()
        {
            var table = new DataTable();
            table.Columns.Add("tsmc_lot");
            table.Rows.Add("O'LOT");

            DataRow[] rows = table.Select("tsmc_lot='" + TsmcIeda.EscapeDataTableFilterValue("O'LOT") + "'");

            Assert.Single(rows);
        }

        private static object AnonymousParameterValue(object parameters, string name)
        {
            return parameters.GetType().GetProperty(name).GetValue(parameters);
        }

        private static bool DynamicParametersContain(DynamicParameters parameters, string expected)
        {
            return parameters.ParameterNames.Any(name => Convert.ToString(parameters.Get<object>(name)) == expected);
        }
    }
}
