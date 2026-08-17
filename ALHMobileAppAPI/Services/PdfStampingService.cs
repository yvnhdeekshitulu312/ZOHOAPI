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
    public class PdfStampingService : IPdfStampingService
    {
        public Task<byte[]> StampAsync(Stream sourcePdf, IList<EsignField> fields, IList<FieldStampInput> values)
        {
            using (var document = PdfReader.Open(sourcePdf, PdfDocumentOpenMode.Modify))
            {
                var valueLookup = values.ToDictionary(v => v.FieldId);

                foreach (var field in fields)
                {
                    if (!valueLookup.TryGetValue(field.Id, out var input) || string.IsNullOrEmpty(input.Value))
                    {
                        if (field.IsRequired)
                            throw new InvalidOperationException($"Required field {field.Id} ({field.FieldType}) has no value.");
                        continue;
                    }

                    var page = document.Pages[field.PageNumber - 1];

                    // Account for page rotation -- visual width/height (what the preview
                    // image and XPct/YPct were captured against) differ from the raw
                    // MediaBox when Rotate is 90 or 270.
                    double pageWidth = page.Width.Point;
                    double pageHeight = page.Height.Point;
                    if (page.Rotate == 90 || page.Rotate == 270)
                    {
                        pageWidth = page.Height.Point;
                        pageHeight = page.Width.Point;
                    }

                    using (var gfx = XGraphics.FromPdfPage(page))
                    {
                        double xPt = (double)(field.XPct / 100m) * pageWidth;
                        double yPt = (double)(field.YPct / 100m) * pageHeight;
                        double wPt = (double)(field.WidthPct / 100m) * pageWidth;
                        double hPt = (double)(field.HeightPct / 100m) * pageHeight;

                        switch (field.FieldType)
                        {
                            case FieldType.Signature:
                            case FieldType.Stamp:
                            case FieldType.Initial:
                                DrawImageField(gfx, input.Value, xPt, yPt, wPt, hPt, pageHeight);
                                break;

                            case FieldType.Text:
                            case FieldType.DateTime: 
                                DrawTextField(gfx, input.Value, xPt, yPt, hPt, pageHeight);
                                break;

                            case FieldType.Checkbox:
                                DrawTextField(gfx, input.Value == "true" ? "X" : "", xPt, yPt, hPt, pageHeight);
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

        private static void DrawImageField(XGraphics gfx, string base64Png, double x, double y, double w, double h, double pageHeight)
        {
            var commaIndex = base64Png.IndexOf(',');
            var raw = commaIndex >= 0 ? base64Png.Substring(commaIndex + 1) : base64Png;
            var bytes = Convert.FromBase64String(raw);

            using (var ms = new MemoryStream(bytes))
            {
                var image = XImage.FromStream(() => ms);
                double pdfY = pageHeight - y - h;
                gfx.DrawImage(image, x, pdfY, w, h);
            }
        }

        private static void DrawTextField(XGraphics gfx, string text, double x, double y, double boxHeight, double pageHeight)
        {
            var font = new XFont("Arial", 10, XFontStyle.Regular);
            double pdfY = pageHeight - y - boxHeight;
            gfx.DrawString(text ?? string.Empty, font, XBrushes.Black,
                new XRect(x, pdfY, 300, boxHeight), XStringFormats.TopLeft);
        }
    }
}