using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using PdfiumViewer;

namespace ALHMobileAppAPI.Esign.Services
{
    /// <summary>
    /// Free (Apache 2.0) replacement for AsposePdfRenderingService — works on
    /// .NET Framework 4.6.1 (PdfiumViewer targets net45+), unlike PDFtoImage
    /// which requires net471+. Same interface, same behavior: one base64
    /// JPEG per page, in page order.
    /// </summary>
    public class PdfiumViewerRenderingService : IPdfRenderingService
    {
        private const int Dpi = 400; // final output DPI -- helps small form/signature text stay legible

        public Task<List<string>> RenderPagesAsync(Stream pdfStream)
        {
            var base64Pages = new List<string>();

            using (var document = PdfDocument.Load(pdfStream))
            {
                for (int i = 0; i < document.PageCount; i++)
                {
                    // PdfRenderFlags.CorrectFromDpi is what actually makes PdfiumViewer
                    // scale the output to the requested DPI -- without it, Render()
                    // silently ignores dpiX/dpiY and renders at the PDF's raw point
                    // size (1px/pt, i.e. 72 DPI), which is why every earlier DPI/quality
                    // change had zero visible effect. See PdfDocument.cs source: the
                    // width/height *= dpi/72 correction only runs when this flag is set.
                    using (var image = document.Render(
                        i,
                        Dpi,
                        Dpi,
                        PdfRenderFlags.Annotations | PdfRenderFlags.CorrectFromDpi))
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            // PNG = lossless
                            image.Save(memoryStream, ImageFormat.Png);

                            base64Pages.Add(
                                Convert.ToBase64String(memoryStream.ToArray())
                            );
                        }
                    }
                }
            }

            return Task.FromResult(base64Pages);
        }
    }
}
