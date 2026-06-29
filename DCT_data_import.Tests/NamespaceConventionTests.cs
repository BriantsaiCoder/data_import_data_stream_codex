using System;
using System.Collections.Generic;
using DCT_data_import.Common;
using DCT_data_import.DbApi;
using DCT_data_import.FileAccess;
using DCT_data_import.MySqlApi;
using DCT_data_import.ReadAndImport;
using Xunit;

namespace DCT_data_import.Tests
{
    public class NamespaceConventionTests
    {
        public static IEnumerable<object[]> ProductionNamespaceCases()
        {
            yield return new object[] { typeof(Program), "DCT_data_import" };
            yield return new object[] { typeof(ImportDecision), "DCT_data_import" };

            yield return new object[] { typeof(WriteToLog), "DCT_data_import.Common" };
            yield return new object[] { typeof(LogLevel), "DCT_data_import.Common" };
            yield return new object[] { typeof(EmailModels), "DCT_data_import.Common" };
            yield return new object[] { typeof(RuntimeMode), "DCT_data_import.Common" };
            yield return new object[] { typeof(NotificationService), "DCT_data_import.Common" };
            yield return new object[] { typeof(CalculateSPC), "DCT_data_import.Common" };
            yield return new object[] { typeof(StatisticItem), "DCT_data_import.Common" };

            yield return new object[] { typeof(FileProcess), "DCT_data_import.FileAccess" };
            yield return new object[] { typeof(CsvColumnNames), "DCT_data_import.FileAccess" };
            yield return new object[] { typeof(ReadWriteINIfile), "DCT_data_import.FileAccess" };
            yield return new object[] { typeof(RawDataContentFormat), "DCT_data_import.FileAccess" };
            yield return new object[] { typeof(TestStatusContentFormat), "DCT_data_import.FileAccess" };
            yield return new object[] { typeof(RecoveryRateDataContentFormat), "DCT_data_import.FileAccess" };
            yield return new object[] { typeof(UIStatusContentFormat), "DCT_data_import.FileAccess" };
            yield return new object[] { typeof(FailPinLogContentFormat), "DCT_data_import.FileAccess" };
            yield return new object[] { typeof(IedaDataFormat), "DCT_data_import.FileAccess" };

            yield return new object[] { typeof(DatabaseService), "DCT_data_import.DbApi" };
            yield return new object[] { typeof(DbAccess), "DCT_data_import.DbApi" };
            yield return new object[] { typeof(DbObject), "DCT_data_import.DbApi" };
            yield return new object[] { typeof(DbObject.DbSqlRequest), "DCT_data_import.DbApi" };
            yield return new object[] { typeof(DbObject.DbQueryResult), "DCT_data_import.DbApi" };
            yield return new object[] { typeof(DbObject.DbCommandResult), "DCT_data_import.DbApi" };
            yield return new object[] { typeof(DbObject.ImportResult), "DCT_data_import.DbApi" };
            yield return new object[] { typeof(DbObject.DbKeyObject), "DCT_data_import.DbApi" };

            yield return new object[] { typeof(DBmysql), "DCT_data_import.MySqlApi" };
            yield return new object[] { typeof(MySqlConnectionManager), "DCT_data_import.MySqlApi" };

            yield return new object[] { typeof(ImportData), "DCT_data_import.ReadAndImport" };
            yield return new object[] { typeof(FileCheckResult), "DCT_data_import.ReadAndImport" };
            yield return new object[] { typeof(RawData), "DCT_data_import.ReadAndImport" };
            yield return new object[] { typeof(MultiSpecRawData), "DCT_data_import.ReadAndImport" };
            yield return new object[] { typeof(Tester), "DCT_data_import.ReadAndImport" };
            yield return new object[] { typeof(RecoveryRate), "DCT_data_import.ReadAndImport" };
            yield return new object[] { typeof(FailPin), "DCT_data_import.ReadAndImport" };
            yield return new object[] { typeof(UiStatus), "DCT_data_import.ReadAndImport" };
            yield return new object[] { typeof(TsmcIeda), "DCT_data_import.ReadAndImport" };
        }

        [Theory]
        [MemberData(nameof(ProductionNamespaceCases))]
        public void ProductionTypes_UseFolderAlignedNamespace(Type type, string expectedNamespace)
        {
            Assert.Equal(expectedNamespace, type.Namespace);
        }
    }
}
