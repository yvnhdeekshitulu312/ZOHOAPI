using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf.IO;
using ALHMobileAppAPI.Esign.Models;

namespace ALHMobileAppAPI.Esign.Services
{
    /// <summary>
    /// .NET Framework 4.8 equivalent of the "pdf-lib" stamping step in the
    /// original Node.js guide. Install via NuGet: PdfSharpCore (+ SixLabors.Fonts
    /// dependency pulls in automatically). Works cross-platform, no native deps.
    /// </summary>
    public class PdfStampingService : IPdfStampingService
    {
        public Task<byte[]> StampAsync(Stream sourcePdf, IList<EsignField> fields, IList<FieldStampInput> values)
        {
            using (var document = PdfReader.Open(sourcePdf, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.Modify))
            {
                var valueLookup = values.ToDictionary(v => v.FieldId);

                foreach (var field in fields)
                {
                    if (!valueLookup.TryGetValue(field.Id, out var input) || string.IsNullOrEmpty(input.Value))
                    {
                        if (field.IsRequired)
                        {
                            throw new InvalidOperationException(
                                $"Required field {field.Id} ({field.FieldType}) has no value.");
                        }
                        continue;
                    }

                    // Pages are 0-indexed in PdfSharpCore, your PageNumber column is 1-indexed
                    var page = document.Pages[field.PageNumber - 1];
                    using (var gfx = XGraphics.FromPdfPage(page))
                    {
                        // Convert stored percentages back to points for this specific page size
                        double xPt = (double)(field.XPct / 100m) * page.Width.Point;
                        double yPt = (double)(field.YPct / 100m) * page.Height.Point;
                        double wPt = (double)(field.WidthPct / 100m) * page.Width.Point;
                        double hPt = (double)(field.HeightPct / 100m) * page.Height.Point;

                        switch (field.FieldType)
                        {
                            case FieldType.Signature:
                            case FieldType.Stamp:
                                DrawImageField(gfx, input.Value, xPt, yPt, wPt, hPt);
                                break;

                            case FieldType.Text:
                            case FieldType.Date:
                                DrawTextField(gfx, input.Value, xPt, yPt, hPt);
                                break;

                            case FieldType.Initial:
                                DrawImageField(gfx, input.Value, xPt, yPt, wPt, hPt);
                                break;

                            case FieldType.Checkbox:
                                DrawTextField(gfx, input.Value == "true" ? "X" : "", xPt, yPt, hPt);
                                break;
                        }
                    }
                }

                using (var output = new MemoryStream())
                {
                    document.Save(output, false);
                    return Task.FromResult(output.ToArray());
                }
            }
        }

        private static void DrawImageField(XGraphics gfx, string base64Png, double x, double y, double w, double h)
        {
            // Expecting "data:image/png;base64,...." from signature_pad on the frontend
            var commaIndex = base64Png.IndexOf(',');
            var raw = commaIndex >= 0 ? base64Png.Substring(commaIndex + 1) : base64Png;
            var bytes = Convert.FromBase64String(raw);

            using (var ms = new MemoryStream(bytes))
            {
                var image = XImage.FromStream(() => ms);
                // PDF Y origin is bottom-left, our stored Y is top-left (matches screen/canvas) -- flip it
                double pdfY = gfx.PdfPage.Height.Point - y - h;
                gfx.DrawImage(image, x, pdfY, w, h);
            }
        }

        private static void DrawTextField(XGraphics gfx, string text, double x, double y, double boxHeight)
        {
            var font = new XFont("Arial", 10, XFontStyle.Regular);
            double pdfY = gfx.PdfPage.Height.Point - y - boxHeight;
            gfx.DrawString(text ?? string.Empty, font, XBrushes.Black,
                new XRect(x, pdfY, 300, boxHeight), XStringFormats.TopLeft);
        }
    }
}
