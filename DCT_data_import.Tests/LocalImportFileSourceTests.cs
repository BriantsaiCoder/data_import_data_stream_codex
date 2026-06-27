using System;
using System.IO;
using System.Linq;
using DCT_data_import;
using DCT_data_import.ReadAndImport;
using Xunit;

namespace DCT_data_import.Tests
{
    [Collection("RuntimeMode")]
    public class LocalImportFileSourceTests : IDisposable
    {
        private readonly string _root;

        public LocalImportFileSourceTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "dct-local-import-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            RuntimeMode.SetDryRunOverrideForTests(null);
            ImportSourceSettings.ClearOverridesForTests();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Fact]
        public void ParseImportSource_WhenMissingOrUnknown_DefaultsToFtp()
        {
            Assert.Equal(ImportSourceKind.Ftp, ImportSourceSettings.ParseImportSource(null));
            Assert.Equal(ImportSourceKind.Ftp, ImportSourceSettings.ParseImportSource("Unknown"));
            Assert.Equal(ImportSourceKind.Local, ImportSourceSettings.ParseImportSource("Local"));
        }

        [Fact]
        public void ParseLocalSuccessAction_WhenMissingOrUnknown_DefaultsToDelete()
        {
            Assert.Equal(LocalSuccessAction.Delete, ImportSourceSettings.ParseLocalSuccessAction(null));
            Assert.Equal(LocalSuccessAction.Delete, ImportSourceSettings.ParseLocalSuccessAction("Unknown"));
            Assert.Equal(LocalSuccessAction.Archive, ImportSourceSettings.ParseLocalSuccessAction("Archive"));
        }

        [Fact]
        public void Create_WhenLocalRootIsEmpty_ThrowsClearError()
        {
            ImportSourceSettings.SetOverridesForTests("Local", "", "Delete");

            var ex = Assert.Throws<InvalidOperationException>(() => ImportFileSourceFactory.Create());

            Assert.Contains("LocalImportRoot", ex.Message);
        }

        [Fact]
        public void LocalSource_CanCheckOpenAndMeasureFile()
        {
            var source = new LocalImportFileSource(_root, LocalSuccessAction.Delete);
            string path = source.GetPath("Data_Cloud_CSV/test_result_db.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "hello");

            Assert.True(source.Exists(path));
            Assert.Equal(5, source.GetLength(path));
            using (var reader = new StreamReader(source.OpenRead(path)))
            {
                Assert.Equal("hello", reader.ReadToEnd());
            }
        }

        [Fact]
        public void GetPath_WhenRelativePathEscapesRoot_ThrowsClearError()
        {
            var source = new LocalImportFileSource(_root, LocalSuccessAction.Delete);

            var ex = Assert.Throws<InvalidOperationException>(() => source.GetPath("../outside.csv"));

            Assert.Contains("LocalImportRoot", ex.Message);
        }

        [Fact]
        public void OpenRead_WhenPathEscapesRoot_ThrowsClearError()
        {
            var source = new LocalImportFileSource(_root, LocalSuccessAction.Delete);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                source.OpenRead(Path.Combine(_root, "..", "outside.csv")));

            Assert.Contains("LocalImportRoot", ex.Message);
        }

        [Fact]
        public void ListFiles_UsesExistingMultiSpecPattern()
        {
            var source = new LocalImportFileSource(_root, LocalSuccessAction.Delete);
            string directory = source.GetPath("Data_Cloud_CSV_MultiSpec/");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "test_result_site1_DB001.csv"), "a");
            File.WriteAllText(Path.Combine(directory, "test_result_siteA_DB001.csv"), "b");
            File.WriteAllText(Path.Combine(directory, "other.csv"), "c");

            var files = source.ListFiles(directory, "test_result_site*_DB001.csv");

            Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), directory);
            Assert.True(File.Exists(directory + "test_result_site1_DB001.csv"));
            Assert.Equal(new[] { "test_result_site1_DB001.csv" }, files.OrderBy(x => x).ToArray());
        }

        [Fact]
        public void CompleteSuccess_WhenDelete_RemovesSourceFile()
        {
            var source = new LocalImportFileSource(_root, LocalSuccessAction.Delete);
            string path = source.GetPath("Tester_Status/tester_DB001.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "ok");

            source.CompleteSuccess(path);

            Assert.False(File.Exists(path));
        }

        [Fact]
        public void CompleteSuccess_WhenArchive_MovesToImportedAndKeepsExistingFile()
        {
            var source = new LocalImportFileSource(_root, LocalSuccessAction.Archive);
            string path = source.GetPath("Tester_Status/tester_DB001.csv");
            string archivedPath = Path.Combine(_root, "Imported", "Tester_Status", "tester_DB001.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Directory.CreateDirectory(Path.GetDirectoryName(archivedPath));
            File.WriteAllText(path, "new");
            File.WriteAllText(archivedPath, "old");

            source.CompleteSuccess(path);

            Assert.False(File.Exists(path));
            Assert.Equal("old", File.ReadAllText(archivedPath));
            Assert.Equal(2, Directory.GetFiles(Path.GetDirectoryName(archivedPath), "tester_DB001*.csv").Length);
        }

        [Fact]
        public void MoveToError_MovesFileToErrorPath()
        {
            var source = new LocalImportFileSource(_root, LocalSuccessAction.Delete);
            string path = source.GetPath("UI_Status/ui_status_DB001.csv");
            string errorPath = source.GetPath("UI_Status_Error/ui_status_DB001.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "bad");

            source.MoveToError(path, errorPath);

            Assert.False(File.Exists(path));
            Assert.True(File.Exists(errorPath));
        }

        [Fact]
        public void MoveToError_WhenErrorPathEscapesRoot_ThrowsClearError()
        {
            var source = new LocalImportFileSource(_root, LocalSuccessAction.Delete);
            string path = source.GetPath("UI_Status/ui_status_DB001.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "bad");

            var ex = Assert.Throws<InvalidOperationException>(() =>
                source.MoveToError(path, Path.Combine(_root, "..", "outside.csv")));

            Assert.Contains("LocalImportRoot", ex.Message);
            Assert.True(File.Exists(path));
        }

        [Fact]
        public void DryRun_DoesNotDeleteOrMoveLocalFiles()
        {
            RuntimeMode.SetDryRunOverrideForTests(true);
            var source = new LocalImportFileSource(_root, LocalSuccessAction.Delete);
            string successPath = source.GetPath("Tester_Status/tester_DB001.csv");
            string errorSourcePath = source.GetPath("UI_Status/ui_status_DB001.csv");
            string errorPath = source.GetPath("UI_Status_Error/ui_status_DB001.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(successPath));
            Directory.CreateDirectory(Path.GetDirectoryName(errorSourcePath));
            File.WriteAllText(successPath, "ok");
            File.WriteAllText(errorSourcePath, "bad");

            source.CompleteSuccess(successPath);
            source.MoveToError(errorSourcePath, errorPath);

            Assert.True(File.Exists(successPath));
            Assert.True(File.Exists(errorSourcePath));
            Assert.False(File.Exists(errorPath));
        }
    }
}
