using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Aspose.Pdf;
using Aspose.Pdf.Devices;

namespace ALHMobileAppAPI.Esign.Services
{
    public interface IPdfRenderingService
    {
        /// Renders every page of the PDF to a JPEG, returned as base64 strings in page order.
        Task<List<string>> RenderPagesAsync(Stream pdfStream);
    }

    /// <summary>
    /// Same approach as your existing ALHMobileAppAPI.SignatureController.PdfToImage,
    /// just moved into a reusable service so both the old and new modules can call it.
    /// </summary>
    public class AsposePdfRenderingService : IPdfRenderingService
    {
        public Task<List<string>> RenderPagesAsync(Stream pdfStream)
        {
            var base64Pages = new List<string>();
            var document = new Document(pdfStream);

            for (int i = 1; i <= document.Pages.Count; i++)
            {
                using (var memoryStream = new MemoryStream())
                {
                    var renderer = new JpegDevice(150); // 150 DPI -- bump if field text looks blurry
                    renderer.Process(document.Pages[i], memoryStream);
                    base64Pages.Add(System.Convert.ToBase64String(memoryStream.ToArray()));
                }
            }

            return Task.FromResult(base64Pages);
        }
    }
}
