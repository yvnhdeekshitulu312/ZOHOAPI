using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;

namespace ALHMobileAppAPI.Esign.Services
{
    /// <summary>
    /// Stores uploaded/stamped PDFs on local disk under App_Data/EsignFiles.
    /// Swap for a GCS-backed IFileStorageService whenever that's wired up --
    /// nothing else in the module depends on this class directly.
    /// </summary>
    public class LocalDiskFileStorageService : IFileStorageService
    {
        private readonly string _filesFolder;

        public LocalDiskFileStorageService()
        {
            _filesFolder = HttpContext.Current != null
                ? HttpContext.Current.Server.MapPath("~/App_Data/EsignFiles")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "EsignFiles");

            if (!Directory.Exists(_filesFolder))
                Directory.CreateDirectory(_filesFolder);
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
        {
            var safeName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
            var fullPath = Path.Combine(_filesFolder, safeName);

            using (var output = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                await fileStream.CopyToAsync(output);
            }

            return safeName;
        }

        public Task<Stream> DownloadAsync(string path)
        {
            var fullPath = Path.Combine(_filesFolder, path);
            Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            return Task.FromResult(stream);
        }

        public Task<string> GetViewerUrlAsync(string path)
        {
            // Served via API/Esign/GetFile/{fileName} -- see EsignFilesController
            return Task.FromResult($"/API/Esign/GetFile/{path}");
        }

        public Task DeleteAsync(string path)
        {
            var fullPath = Path.Combine(_filesFolder, path);
            if (File.Exists(fullPath)) File.Delete(fullPath);
            return Task.CompletedTask;
        }
    }
}
