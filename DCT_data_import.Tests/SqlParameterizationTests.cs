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
        public void BuildDbKeyStatusSelectQuery_KeepsDbKeyOutOfSqlText()
        {
            Execute_query query = DbAccess.BuildDbKeyStatusSelectQuery("db_key", Payload);

            Assert.Contains("@dbKey", query.Query);
            Assert.DoesNotContain(Payload, query.Query);
            Assert.Equal(Payload, AnonymousParameterValue(query.Parameters, "dbKey"));
        }

        [Fact]
        public void BuildDbKeyImportStatusUpdateQuery_KeepsRemarkAndDbKeyOutOfSqlText()
        {
            Execute_query query = DbAccess.BuildDbKeyImportStatusUpdateQuery(Payload, 1, 1, 1, 1, Payload, "1", "0");

            Assert.Contains("@remark", query.Query);
            Assert.Contains("@dbKey", query.Query);
            Assert.DoesNotContain(Payload, query.Query);
            Assert.Equal(Payload, AnonymousParameterValue(query.Parameters, "remark"));
            Assert.Equal(Payload, AnonymousParameterValue(query.Parameters, "dbKey"));
        }

        [Fact]
        public void BuildTableExistsQuery_KeepsTableNameOutOfSqlText()
        {
            Execute_query query = DatabaseService.BuildTableExistsQuery("dct", Payload);

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
            Execute_query query = FileProcess.BuildInsertQuery("lots_result", "`lot_id`", values, parameters);

            Assert.Contains("@batch_0),(@batch_1", query.Query);
            Assert.DoesNotContain(csvPayload, query.Query);
            Assert.True(DynamicParametersContain((DynamicParameters)query.Parameters, csvPayload));
        }

        [Fact]
        public void BuildIedaTitleInsertQuery_KeepsFileValuesOutOfSqlText()
        {
            var content = new IedaDataFormat();
            DataRow row = content.IedaTitle.NewRow();
            row["lot_id"] = Payload;
            content.IedaTitle.Rows.Add(row);

            Execute_query query = TsmcIeda.BuildIedaTitleInsertQuery(content.IedaTitle, new FileProcess());

            Assert.Contains("@title_", query.Query);
            Assert.DoesNotContain(Payload, query.Query);
            Assert.True(DynamicParametersContain((DynamicParameters)query.Parameters, Payload));
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
