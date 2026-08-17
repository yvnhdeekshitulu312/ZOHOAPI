using System;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using ALHMobileAppAPI.Esign.Services;

namespace ALHMobileAppAPI.Esign.Services
{
    /// <summary>
    /// Mirrors RCMAPI.Controllers.Files.FileController's GCS calling pattern exactly
    /// (same credential loading, same scope, same Web.config keys) so this project
    /// doesn't introduce a second, differently-configured GCS client into the solution.
    /// </summary>
    public class GcsFileStorageService : IFileStorageService
    {
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;
        private const string BaseFolder = "Esign/Documents";

        public GcsFileStorageService()
        {
            var credentialsPath = ConfigurationManager.AppSettings["GCS.CredentialsPath"];
            if (string.IsNullOrEmpty(credentialsPath))
                throw new InvalidOperationException("GCS.CredentialsPath is not configured in Web.config");

            _bucketName = ConfigurationManager.AppSettings["GCS.BucketName"];
            if (string.IsNullOrEmpty(_bucketName))
                throw new InvalidOperationException("GCS.BucketName is not configured in Web.config");

            var credential = GoogleCredential
                .FromFile(credentialsPath)
                .CreateScoped("https://www.googleapis.com/auth/devstorage.read_write");

            _storageClient = StorageClient.Create(credential);
        }

        private static string GetDateFolderPrefix() => $"{BaseFolder}/{DateTime.Now:yyyy-MM-dd}/";

        private string NormalizeObjectKey(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            return url.Replace("storage.googleapis.com/", "").Replace($"{_bucketName}/", "");
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
        {
            var blobName = GetDateFolderPrefix() +
                           Path.GetFileNameWithoutExtension(fileName) +
                           "-" + Guid.NewGuid() +
                           Path.GetExtension(fileName);

            await _storageClient.UploadObjectAsync(_bucketName, blobName, contentType, fileStream);
            return blobName; // stored as-is in EsignDocuments.OriginalGcsPath/WorkingGcsPath, matching FileController's object-key convention
        }

        public async Task<Stream> DownloadAsync(string gcsPath)
        {
            var objectKey = NormalizeObjectKey(gcsPath);
            var ms = new MemoryStream();
            await _storageClient.DownloadObjectAsync(_bucketName, objectKey, ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public Task<string> GetViewerUrlAsync(string gcsPath)
        {
            // FileController doesn't currently expose signed URLs -- it proxies bytes
            // through DownloadFile instead. Match that pattern: point the viewer at
            // this API's own file-serving endpoint rather than a signed GCS URL.
            var objectKey = NormalizeObjectKey(gcsPath);
            return Task.FromResult($"/API/Esign/GetFile?path={Uri.EscapeDataString(objectKey)}");
        }

        public async Task DeleteAsync(string gcsPath)
        {
            var objectKey = NormalizeObjectKey(gcsPath);
            await _storageClient.DeleteObjectAsync(_bucketName, objectKey);
        }
    }
}