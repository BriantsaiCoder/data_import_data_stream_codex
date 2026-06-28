using System;
using System.Linq;
using MySql.Data.MySqlClient;
using Xunit;

namespace DCT_data_import.Tests
{
    internal sealed class MySqlGoldenMasterFactAttribute : FactAttribute
    {
        public MySqlGoldenMasterFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(MySqlDataDateTimeRoundTripTests.ConnectionStringEnvironmentVariable)))
            {
                Skip = $"{MySqlDataDateTimeRoundTripTests.ConnectionStringEnvironmentVariable} is not set.";
            }
        }
    }

    /// <summary>
    /// MySql.Data 9.4.0 DATETIME materialization golden master.
    /// Set DCT_MYSQL_GOLDEN_MASTER_CONNECTION_STRING to run against a real MySQL database.
    /// </summary>
    public class MySqlDataDateTimeRoundTripTests
    {
        internal const string ConnectionStringEnvironmentVariable = "DCT_MYSQL_GOLDEN_MASTER_CONNECTION_STRING";

        [MySqlGoldenMasterFact]
        public void MySqlData_DateTimeColumns_WhenConnectionStringProvided_RoundTripMatchesGoldenMaster()
        {
            string connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

            DateTime secondPrecision = new DateTime(2022, 6, 6, 13, 8, 22, DateTimeKind.Unspecified);
            DateTime microsecondPrecision = new DateTime(2022, 6, 6, 13, 8, 22, DateTimeKind.Unspecified)
                .AddTicks(1234560L);

            var connectionStringBuilder = new MySqlConnectionStringBuilder(connectionString)
            {
                ConvertZeroDateTime = true,
                Pooling = false
            };

            using var connection = new MySqlConnection(connectionStringBuilder.ConnectionString);
            connection.Open();

            using var databaseCommand = connection.CreateCommand();
            databaseCommand.CommandText = "SELECT DATABASE()";
            string databaseName = databaseCommand.ExecuteScalar() as string;
            Assert.False(string.IsNullOrWhiteSpace(databaseName), $"{ConnectionStringEnvironmentVariable} must include Database.");

            AllowZeroDateForCurrentSession(connection);

            using var createTable = connection.CreateCommand();
            createTable.CommandText = @"
CREATE TEMPORARY TABLE dct_datetime_roundtrip_gm (
    id INT NOT NULL PRIMARY KEY,
    value_datetime DATETIME NOT NULL,
    value_datetime6 DATETIME(6) NOT NULL,
    value_zero DATETIME NOT NULL
) ENGINE=InnoDB";
            createTable.ExecuteNonQuery();

            using var insert = connection.CreateCommand();
            insert.CommandText = @"
INSERT INTO dct_datetime_roundtrip_gm (id, value_datetime, value_datetime6, value_zero)
VALUES (@id, @value_datetime, @value_datetime6, '0000-00-00 00:00:00')";
            insert.Parameters.Add("@id", MySqlDbType.Int32).Value = 1;
            insert.Parameters.Add("@value_datetime", MySqlDbType.DateTime).Value = secondPrecision;
            insert.Parameters.Add("@value_datetime6", MySqlDbType.DateTime).Value = microsecondPrecision;
            Assert.Equal(1, insert.ExecuteNonQuery());

            using var select = connection.CreateCommand();
            select.CommandText = @"
SELECT value_datetime, value_datetime6, value_zero
FROM dct_datetime_roundtrip_gm
WHERE id = @id";
            select.Parameters.Add("@id", MySqlDbType.Int32).Value = 1;

            using var reader = select.ExecuteReader();
            Assert.True(reader.Read());

            DateTime observedSecondPrecision = reader.GetDateTime(0);
            DateTime observedMicrosecondPrecision = reader.GetDateTime(1);
            DateTime observedZeroDate = reader.GetDateTime(2);

            Assert.Equal(secondPrecision, observedSecondPrecision);
            Assert.Equal(DateTimeKind.Unspecified, observedSecondPrecision.Kind);
            Assert.Equal(microsecondPrecision, observedMicrosecondPrecision);
            Assert.Equal(DateTimeKind.Unspecified, observedMicrosecondPrecision.Kind);
            Assert.Equal(DateTime.MinValue, observedZeroDate);
            Assert.Equal(DateTimeKind.Unspecified, observedZeroDate.Kind);
            Assert.False(reader.Read());
        }

        private static void AllowZeroDateForCurrentSession(MySqlConnection connection)
        {
            using var readSqlMode = connection.CreateCommand();
            readSqlMode.CommandText = "SELECT @@SESSION.sql_mode";
            string currentSqlMode = readSqlMode.ExecuteScalar() as string ?? string.Empty;
            string zeroDateSqlMode = string.Join(
                ",",
                currentSqlMode
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Where(mode => mode != "NO_ZERO_DATE"
                        && mode != "NO_ZERO_IN_DATE"
                        && mode != "STRICT_TRANS_TABLES"
                        && mode != "STRICT_ALL_TABLES"));

            using var setSqlMode = connection.CreateCommand();
            setSqlMode.CommandText = "SET SESSION sql_mode = @sql_mode";
            setSqlMode.Parameters.Add("@sql_mode", MySqlDbType.VarChar).Value = zeroDateSqlMode;
            setSqlMode.ExecuteNonQuery();
        }
    }
}
