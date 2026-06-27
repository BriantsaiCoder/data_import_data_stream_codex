using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace DCT_data_import.ReadAndImport
{
    internal enum ImportSourceKind
    {
        Ftp,
        Local
    }

    internal enum LocalSuccessAction
    {
        Delete,
        Archive
    }

    internal interface IImportFileSource
    {
        string GetPath(string relativePath);
        bool Exists(string path);
        List<string> ListFiles(string directoryPath, string filePattern);
        Stream OpenRead(string path);
        long GetLength(string path);
        string CompleteSuccess(string path);
        string MoveToError(string path, string errorPath);
    }

    internal static class ImportSourceSettings
    {
        private static string _importSourceOverride;
        private static string _localImportRootOverride;
        private static string _localSuccessActionOverride;

        public static ImportSourceKind Source =>
            ParseImportSource(_importSourceOverride ?? ConfigurationManager.AppSettings["ImportSource"]);

        public static string LocalImportRoot =>
            _localImportRootOverride ?? ConfigurationManager.AppSettings["LocalImportRoot"];

        public static LocalSuccessAction LocalSuccessAction =>
            ParseLocalSuccessAction(_localSuccessActionOverride ?? ConfigurationManager.AppSettings["LocalSuccessAction"]);

        internal static ImportSourceKind ParseImportSource(string value)
        {
            return string.Equals(value, "Local", StringComparison.OrdinalIgnoreCase)
                ? ImportSourceKind.Local
                : ImportSourceKind.Ftp;
        }

        internal static LocalSuccessAction ParseLocalSuccessAction(string value)
        {
            return string.Equals(value, "Archive", StringComparison.OrdinalIgnoreCase)
                ? LocalSuccessAction.Archive
                : LocalSuccessAction.Delete;
        }

        internal static void SetOverridesForTests(string importSource, string localImportRoot, string localSuccessAction)
        {
            _importSourceOverride = importSource;
            _localImportRootOverride = localImportRoot;
            _localSuccessActionOverride = localSuccessAction;
        }

        internal static void ClearOverridesForTests()
        {
            _importSourceOverride = null;
            _localImportRootOverride = null;
            _localSuccessActionOverride = null;
        }
    }

    internal static class ImportFileSourceFactory
    {
        public static IImportFileSource Create()
        {
            if (ImportSourceSettings.Source == ImportSourceKind.Local)
            {
                return new LocalImportFileSource(
                    ImportSourceSettings.LocalImportRoot,
                    ImportSourceSettings.LocalSuccessAction);
            }

            return CreateFtp();
        }

        private static IImportFileSource CreateFtp()
        {
            return new FtpImportFileSource(
                Program.FTP_IP,
                Program.FTP_USER,
                Program.FTP_PASSWORD,
                Program.Environment);
        }
    }

    internal static class ImportFileNameMatcher
    {
        public static bool IsMatch(string fileName, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return true;
            }

            string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", "\\d+") + "$";
            return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase);
        }
    }

    internal sealed class FtpImportFileSource : IImportFileSource
    {
        private readonly string _ftpIp;
        private readonly string _ftpUser;
        private readonly string _ftpPassword;
        private readonly string _environment;

        public FtpImportFileSource(string ftpIp, string ftpUser, string ftpPassword, string environment)
        {
            _ftpIp = ftpIp;
            _ftpUser = ftpUser;
            _ftpPassword = ftpPassword;
            _environment = environment;
        }

        // ponytail: HttpClient does not support FTP; replace this chokepoint when adopting a dedicated FTP library.
#pragma warning disable SYSLIB0014
        private static FtpWebRequest CreateFtpRequest(string path)
        {
            return (FtpWebRequest)WebRequest.Create(path);
        }

        private static FtpWebRequest CreateFtpRequest(Uri uri)
        {
            return (FtpWebRequest)WebRequest.Create(uri);
        }
#pragma warning restore SYSLIB0014

        public string GetPath(string relativePath)
        {
            string basePath = _environment == "Dev" ? "/DCT_Log/DCT_DB_DATA_Dev/" : "/DCT_Log/DCT_DB_DATA/";
            return "ftp://" + _ftpIp + basePath + relativePath.Replace("\\", "/");
        }

        public bool Exists(string path)
        {
            FtpWebResponse response = null;
            try
            {
                FtpWebRequest request = CreateFtpRequest(path);
                request.Credentials = new NetworkCredential(_ftpUser, _ftpPassword);
                request.Method = WebRequestMethods.Ftp.GetFileSize;
                response = (FtpWebResponse)request.GetResponse();
                return true;
            }
            catch (WebException ex)
            {
                response = ex.Response as FtpWebResponse;
                if (response?.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    return false;
                }

                throw;
            }
            finally
            {
                response?.Close();
            }
        }

        public List<string> ListFiles(string directoryPath, string filePattern)
        {
            var matchingFiles = new List<string>();
            FtpWebRequest request = CreateFtpRequest(directoryPath);
            request.Credentials = new NetworkCredential(_ftpUser, _ftpPassword);
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(responseStream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (ImportFileNameMatcher.IsMatch(line, filePattern))
                    {
                        matchingFiles.Add(line);
                    }
                }
            }
            return matchingFiles;
        }

        public Stream OpenRead(string path)
        {
            FtpWebRequest request = CreateFtpRequest(new Uri(path));
            request.Credentials = new NetworkCredential(_ftpUser, _ftpPassword);
            FtpWebResponse response = (FtpWebResponse)request.GetResponse();
            return new FtpResponseStream(response);
        }

        public long GetLength(string path)
        {
            FtpWebRequest request = CreateFtpRequest(path);
            request.Method = WebRequestMethods.Ftp.GetFileSize;
            request.Credentials = new NetworkCredential(_ftpUser, _ftpPassword);
            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                return response.ContentLength;
            }
        }

        public string CompleteSuccess(string path)
        {
            if (RuntimeMode.IsDryRun)
            {
                return "DryRun: FTP DeleteFile skipped";
            }

            FtpWebRequest request = CreateFtpRequest(path);
            request.Method = WebRequestMethods.Ftp.DeleteFile;
            request.Credentials = new NetworkCredential(_ftpUser, _ftpPassword);
            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                return response.StatusDescription;
            }
        }

        public string MoveToError(string path, string errorPath)
        {
            if (RuntimeMode.IsDryRun)
            {
                return "DryRun: FTP RenameFile skipped";
            }

            var srcUri = new Uri(path, UriKind.Absolute);
            var dstUri = new Uri(errorPath, UriKind.Absolute);
            var request = CreateFtpRequest(srcUri);
            request.Method = WebRequestMethods.Ftp.Rename;
            request.Credentials = new NetworkCredential(_ftpUser, _ftpPassword);
            request.UsePassive = true;
            request.UseBinary = true;
            request.KeepAlive = false;
            request.Proxy = null;
            request.Timeout = 10000;
            request.RenameTo = Uri.UnescapeDataString(dstUri.AbsolutePath);
            using (var response = (FtpWebResponse)request.GetResponse())
            {
                return response.StatusDescription;
            }
        }
    }

    internal sealed class LocalImportFileSource : IImportFileSource
    {
        private readonly string _root;
        private readonly LocalSuccessAction _successAction;

        public LocalImportFileSource(string root, LocalSuccessAction successAction)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                throw new InvalidOperationException("ImportSource=Local requires LocalImportRoot.");
            }

            _root = Path.GetFullPath(root);
            _successAction = successAction;
        }

        public string GetPath(string relativePath)
        {
            string localRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return NormalizeUnderRoot(Path.Combine(_root, localRelativePath));
        }

        public bool Exists(string path)
        {
            path = NormalizeUnderRoot(path);
            return File.Exists(path);
        }

        public List<string> ListFiles(string directoryPath, string filePattern)
        {
            directoryPath = NormalizeUnderRoot(directoryPath);
            if (!Directory.Exists(directoryPath))
            {
                return new List<string>();
            }

            return Directory.EnumerateFiles(directoryPath)
                .Select(Path.GetFileName)
                .Where(fileName => ImportFileNameMatcher.IsMatch(fileName, filePattern))
                .ToList();
        }

        public Stream OpenRead(string path)
        {
            path = NormalizeUnderRoot(path);
            return File.OpenRead(path);
        }

        public long GetLength(string path)
        {
            path = NormalizeUnderRoot(path);
            return new FileInfo(path).Length;
        }

        public string CompleteSuccess(string path)
        {
            if (RuntimeMode.IsDryRun)
            {
                return "DryRun: Local success skipped";
            }

            path = NormalizeUnderRoot(path);
            if (_successAction == LocalSuccessAction.Archive)
            {
                string archivePath = GetUniquePath(Path.Combine(_root, "Imported", GetRelativePath(path)));
                EnsureDirectory(archivePath);
                File.Move(path, archivePath);
                return "Local Archive completed: " + archivePath;
            }

            File.Delete(path);
            return "Local Delete completed";
        }

        public string MoveToError(string path, string errorPath)
        {
            if (RuntimeMode.IsDryRun)
            {
                return "DryRun: Local MoveToError skipped";
            }

            path = NormalizeUnderRoot(path);
            errorPath = NormalizeUnderRoot(errorPath);
            string targetPath = GetUniquePath(errorPath);
            EnsureDirectory(targetPath);
            File.Move(path, targetPath);
            return "Local MoveToError completed: " + targetPath;
        }

        private string NormalizeUnderRoot(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string rootWithSeparator = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.Equals(_root, StringComparison.OrdinalIgnoreCase)
                && !fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Local import path must stay under LocalImportRoot.");
            }

            return fullPath;
        }

        private string GetRelativePath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string rootWithSeparator = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(rootWithSeparator.Length)
                : Path.GetFileName(path);
        }

        private static void EnsureDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static string GetUniquePath(string path)
        {
            if (!File.Exists(path))
            {
                return path;
            }

            string directory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            string stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string candidate = Path.Combine(directory, fileName + "_" + stamp + extension);
            int counter = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(directory, fileName + "_" + stamp + "_" + counter + extension);
                counter++;
            }

            return candidate;
        }
    }

    internal sealed class FtpResponseStream : Stream
    {
        private readonly FtpWebResponse _response;
        private readonly Stream _inner;

        public FtpResponseStream(FtpWebResponse response)
        {
            _response = response;
            _inner = response.GetResponseStream();
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Close();
            }

            base.Dispose(disposing);
        }
    }
}
