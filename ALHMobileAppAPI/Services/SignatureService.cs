using ALHMobileAppAPI.ALHAppDAL;
using ALHMobileAppAPI.CommonUtilities;
using ALHMobileAppAPI.Models;
using CommanUtilities.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace ALHMobileAppAPI.Services
{
    public class SignatureService
    {
        // =========================================================
        // Login
        // =========================================================

        public LoginDetails ValidateLoginCredentials(
            string username,
            string password)
        {
            SignatureDAL dal = new SignatureDAL();

            return dal.ValidateLoginCredentials(
                username,
                password);
        }

        // =========================================================
        // Save Signature Request - ASYNC
        // =========================================================

        public async Task<Base> SaveSignatureRequestsAsync(
            SignatureModel sigParams,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SignatureDAL dal = new SignatureDAL();

            // -----------------------------------------------------
            // Save first
            // -----------------------------------------------------

            var result =
                dal.SaveSignatureRequests(sigParams);

            // -----------------------------------------------------
            // Only send email after successful save
            // -----------------------------------------------------

            if (result != null &&
                result.Code == CommanUtilities.Models.ProcessStatus.Success)
            {
                try
                {
                    await SendSignatureEmailsAsync(
                        sigParams,
                        cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Request was cancelled.
                    // Do not turn successful DB operation into failure.
                    HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(
                        new Exception(
                            "Signature email operation was cancelled."),
                        "SignatureService",
                        "Email cancellation",
                        "");
                }
                catch (Exception ex)
                {
                    // Email failure must NOT break the successful
                    // signature request.
                    HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(
                        ex,
                        "SignatureService",
                        "Error sending signature emails",
                        "");
                }
            }

            return result;
        }

        // =========================================================
        // Fetch
        // =========================================================

        public Base FetchSignatureRequests(
            string RequestId)
        {
            SignatureDAL dal = new SignatureDAL();

            return dal.FetchSignatureRequests(
                RequestId);
        }

        // =========================================================
        // Fetch recipients
        // =========================================================

        public Base FetchSSSignatureReciepientUsers(
            string name)
        {
            SignatureDAL dal = new SignatureDAL();

            return dal.FetchSSSignatureReciepientUsers(
                name);
        }

        // =========================================================
        // Send Signature Emails
        // =========================================================

        private async Task SendSignatureEmailsAsync(
            SignatureModel sig,
            CancellationToken cancellationToken)
        {
            if (sig == null)
                return;

            if (sig.ReciepientsXML == null)
                return;

            var recips =
                sig.ReciepientsXML
                    .Cast<object>()
                    .ToList();

            if (recips.Count == 0)
                return;

            string baseUrl =
                ConfigurationManager
                    .AppSettings["EsignAppBaseUrl"]
                ?? string.Empty;

            string contactEmail =
                ConfigurationManager
                    .AppSettings["EsignContactEmail"]
                ?? string.Empty;

            string organizationName =
                ConfigurationManager
                    .AppSettings["EsignOrganizationName"]
                ?? "Al Hammadi Hospitals";

            string docName =
                string.IsNullOrWhiteSpace(
                    sig.RequestDocumentName)
                    ? "a document"
                    : sig.RequestDocumentName;

            // -----------------------------------------------------
            // RequestedBy / ExpiresOn / MessageToAll / PrivateMessage
            // are not yet columns on SignatureModel. GetProp() is
            // reused here so that as soon as a matching property is
            // added to the model (any of the names tried below),
            // the email starts picking it up automatically with no
            // further code changes.
            // -----------------------------------------------------

            string requestedBy =
                GetProp(
                    sig,
                    "RequestedBy",
                    "RequesterName",
                    "SenderName",
                    "CreatedBy",
                    "RequestedByName",
                    "SentBy")
                ?? "A colleague at " + organizationName;

            string expiresOnRaw =
                GetProp(
                    sig,
                    "ExpiresOn",
                    "ExpiryDate",
                    "ExpirationDate",
                    "ValidTill",
                    "ValidUntil");

            string expiresOnText =
                FormatExpiry(expiresOnRaw);

            string messageToAll =
                GetProp(
                    sig,
                    "Notes",
                    "Note",
                    "MessageToAll",
                    "Message");

            string privateMessage =
                GetProp(
                    sig,
                    "PrivateMessage",
                    "PrivateNote");

            var targets = recips
    .Select(r => new RecipientEmailTarget
    {
        Email = GetProp(
            r,
            "EMAIL",
            "Email",
            "EmailId",
            "EmailAddress"),

        Name = GetProp(
            r,
            "NAME",
            "Name",
            "ReciepientName",
            "RecipientName",
            "FullName"),

        Order = ParseInt(
            GetProp(
                r,
                "SigningOrder",
                "SendingOrder",
                "Order",
                "SigningorderId"))
    })
    .Where(x => !string.IsNullOrWhiteSpace(x.Email))
    .ToList();

            if (targets.Count == 0)
                return;

            // -----------------------------------------------------
            // Determine who gets email
            // -----------------------------------------------------

            List<RecipientEmailTarget> toNotify;

            if (sig.SendInOrder)
            {
                toNotify =
                    targets
                        .OrderBy(t =>
                            t.Order == 0
                                ? int.MaxValue
                                : t.Order)
                        .Take(1)
                        .ToList();
            }
            else
            {
                toNotify = targets;
            }



            foreach (var target in toNotify)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string subject =
                    "Signature requested: " +
                    docName;

                string body =
                    BuildEmailBody(
                        target.Name,
                        docName,
                        baseUrl,
                        requestedBy,
                        organizationName,
                        expiresOnText,
                        contactEmail,
                        messageToAll,
                        privateMessage);

                try
                {
                    bool sent =
                        await EmailHelper
                            .SendEmailAsync(
                                target.Email,
                                target.Name,
                                subject,
                                body,
                                cancellationToken)
                            .ConfigureAwait(false);

                    if (!sent)
                    {
                        HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(
                            new Exception(
                                "EmailHelper returned false."),
                            "SignatureService",
                            "Email was not sent",
                            target.Email);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // One recipient failing should not prevent
                    // the next recipient from being processed.
                    HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(
                        ex,
                        "SignatureService",
                        "Error sending email to " +
                        target.Email,
                        "");
                }
            }
        }

        // =========================================================
        // Email body -- "Brand Card" design
        //
        // Table-based HTML (works in Outlook desktop, Outlook Web,
        // Gmail, Apple Mail) matching the approved mockup's
        // "S · Brand Card" variation: logo header, gradient
        // ACTION REQUIRED banner, Sender/Organization/Expires on/
        // Message to all/Private message rows, single Start Signing
        // button, footer.
        //
        // Uses only approved brand colors and Noto Kufi Arabic
        // (falls back to Arial where the font isn't available on
        // the recipient's device, since custom fonts are not
        // reliably loaded by all email clients). The header-band
        // gradient degrades gracefully to a flat Brand Blue
        // background on clients (e.g. Outlook desktop) that ignore
        // CSS background-image.
        //
        // The logo is referenced as cid:{EmailHelper.LogoContentId}
        // -- EmailHelper.SendEmailAsync attaches the actual image
        // file as an inline resource with that Content-Id.
        // =========================================================

        private static string BuildEmailBody(
            string name,
            string docName,
            string baseUrl,
            string requestedBy,
            string organizationName,
            string expiresOnText,
            string contactEmail,
            string messageToAll,
            string privateMessage)
        {
            string safeDocName =
                HttpUtility.HtmlEncode(docName);

            string safeRequestedBy =
                HttpUtility.HtmlEncode(requestedBy);

            string safeOrganization =
                HttpUtility.HtmlEncode(organizationName);

            string signLink =
                string.IsNullOrEmpty(baseUrl)
                    ? "#"
                    : HttpUtility.HtmlAttributeEncode(baseUrl);

            // -----------------------------------------------------
            // EXPIRES ON row (only rendered when we have a value --
            // unlike Message to all / Private message, which always
            // render with an em dash when empty, this one is a
            // genuinely-not-collected-yet field on SignatureModel)
            // -----------------------------------------------------

            string expiresRow =
                string.IsNullOrWhiteSpace(expiresOnText)
                    ? string.Empty
                    : DetailRow("&#9201;", "EXPIRES ON", HttpUtility.HtmlEncode(expiresOnText), false);

            string expiresFooterLine =
                string.IsNullOrWhiteSpace(expiresOnText)
                    ? string.Empty
                    :
                    "<div style=\"font-size:12px;color:#969696;margin-top:10px;\">" +
                    "This link expires on " +
                    HttpUtility.HtmlEncode(expiresOnText) +
                    "</div>";

            // -----------------------------------------------------
            // Footer contact line
            // -----------------------------------------------------

            string contactLine =
                string.IsNullOrWhiteSpace(contactEmail)
                    ?
                    "This is an automated email from the " +
                    safeOrganization +
                    " Signature Portal."
                    :
                    "This is an automated email from the " +
                    safeOrganization +
                    " Signature Portal. For queries, contact " +
                    "<a href=\"mailto:" +
                    HttpUtility.HtmlAttributeEncode(contactEmail) +
                    "\" style=\"color:#1855A4;text-decoration:none;\">" +
                    HttpUtility.HtmlEncode(contactEmail) +
                    "</a> directly.";

            return
$@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#E5E5E5;padding:24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""560"" cellpadding=""0"" cellspacing=""0"" style=""background:#FFFFFF;border-radius:16px;overflow:hidden;font-family:'Noto Kufi Arabic',Arial,sans-serif;"">

        <!-- Header / logo -->
        <tr>
          <td style=""padding:26px 32px;border-bottom:1px solid #E5E5E5;"">
            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"">
              <tr>
                <td style=""width:40px;height:40px;background:#1855A4;border-radius:10px;text-align:center;vertical-align:middle;"">
                  <img src=""cid:{EmailHelper.LogoContentId}"" width=""40"" height=""40"" alt=""{safeOrganization}"" style=""display:block;border-radius:10px;border:0;"" />
                </td>
                <td style=""padding-left:12px;"">
                  <div style=""font-weight:800;font-size:15px;color:#002654;"">{safeOrganization}</div>
                  <div style=""font-size:10.5px;color:#969696;font-weight:600;"">Internal Signature Portal</div>
                </td>
              </tr>
            </table>
          </td>
        </tr>

        <!-- Banner (gradient with flat-color fallback for clients that ignore background-image) -->
        <tr>
          <td style=""background-color:#1855A4;background-image:linear-gradient(135deg,#002654,#1855A4);padding:30px 32px;"">
            <span style=""display:inline-block;background:#CDFCFB;color:#002654;font-size:10.5px;font-weight:800;letter-spacing:.06em;text-transform:uppercase;padding:5px 12px;border-radius:100px;"">Action required</span>
            <div style=""color:#FFFFFF;font-size:23px;font-weight:800;margin-top:12px;font-family:'Noto Kufi Arabic',Arial,sans-serif;"">Digital Signature Request</div>
          </td>
        </tr>

        <!-- Request line + detail rows -->
        <tr>
          <td style=""padding:30px 32px 6px 32px;"">
            <div style=""font-size:14px;color:#002654;line-height:1.7;font-family:'Noto Kufi Arabic',Arial,sans-serif;margin-bottom:10px;"">
              {(string.IsNullOrWhiteSpace(name) ? "Hello," : "Dear " + HttpUtility.HtmlEncode(name) + ",")}<br/>
              <b style=""color:#1855A4;"">{safeRequestedBy}</b> has requested you to review and sign <b style=""color:#1855A4;"">{safeDocName}</b>.
            </div>

            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
              {DetailRow("&#9998;", "SENDER", safeRequestedBy, false)}
              {DetailRow("&#127970;", "ORGANIZATION", safeOrganization, false)}
              {expiresRow}
              {DetailRow("&#128172;", "MESSAGE TO ALL", EmptyOrDash(messageToAll), false)}
              {DetailRow("&#128274;", "PRIVATE MESSAGE", EmptyOrDash(privateMessage), true)}
            </table>
          </td>
        </tr>

        <!-- Start Signing button -->
        <tr>
          <td align=""center"" style=""padding:8px 32px 30px 32px;"">
            <a href=""{signLink}"" style=""background:#1855A4;color:#FFFFFF;text-decoration:none;padding:15px 38px;border-radius:11px;font-weight:800;font-size:14.5px;font-family:'Noto Kufi Arabic',Arial,sans-serif;display:inline-block;"">Start Signing</a>
            {expiresFooterLine}
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style=""background:#E5E5E5;padding:22px 32px;font-size:11px;color:#969696;line-height:1.7;font-family:'Noto Kufi Arabic',Arial,sans-serif;"">
            {contactLine}
          </td>
        </tr>

      </table>
    </td>
  </tr>
</table>";
        }

        // =========================================================
        // Single detail row (icon + label + value), matching the
        // Sender / Organization / Expires on / Message to all /
        // Private message rows in the approved mockup. `noBorder`
        // is set for the last row in the stack.
        // =========================================================

        private static string DetailRow(
            string iconHtmlEntity,
            string label,
            string value,
            bool noBorder)
        {
            string borderStyle =
                noBorder
                    ? string.Empty
                    : "border-bottom:1px solid #E5E5E5;";

            return
                "<tr><td style=\"padding:13px 0;" + borderStyle + "\">" +
                "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tr>" +
                "<td style=\"width:32px;height:32px;background:#CDFCFB;color:#002654;border-radius:9px;text-align:center;vertical-align:middle;font-size:14px;line-height:32px;\">" +
                iconHtmlEntity +
                "</td>" +
                "<td style=\"padding-left:14px;\">" +
                "<div style=\"font-size:11px;font-weight:700;color:#969696;text-transform:uppercase;letter-spacing:.04em;font-family:'Noto Kufi Arabic',Arial,sans-serif;\">" +
                HttpUtility.HtmlEncode(label) +
                "</div>" +
                "<div style=\"font-size:13.5px;font-weight:600;color:#002654;font-family:'Noto Kufi Arabic',Arial,sans-serif;\">" +
                value +
                "</div>" +
                "</td>" +
                "</tr></table>" +
                "</td></tr>";
        }

        // =========================================================
        // Renders an em dash for a missing value, HTML-encodes a
        // real one. Used for Message to all / Private message,
        // which the design always shows (never omits the row).
        // =========================================================

        private static string EmptyOrDash(string value)
        {
            return
                string.IsNullOrWhiteSpace(value)
                    ? "&mdash;"
                    : HttpUtility.HtmlEncode(value);
        }

        // =========================================================
        // Recipient helper model
        // =========================================================

        private sealed class RecipientEmailTarget
        {
            public string Email { get; set; }

            public string Name { get; set; }

            public int Order { get; set; }
        }

        // =========================================================
        // Get property
        // =========================================================

        private static string GetProp(
            object obj,
            params string[] names)
        {
            if (obj == null)
                return null;

            // -----------------------------------------------------
            // JObject
            // -----------------------------------------------------

            JObject jo =
                obj as JObject;

            if (jo != null)
            {
                foreach (string name in names)
                {
                    JToken token;

                    if (jo.TryGetValue(
                            name,
                            StringComparison.OrdinalIgnoreCase,
                            out token)
                        &&
                        token != null &&
                        token.Type != JTokenType.Null)
                    {
                        string value =
                            token.ToString();

                        if (!string.IsNullOrWhiteSpace(value))
                            return value;
                    }
                }

                return null;
            }

            // -----------------------------------------------------
            // IDictionary
            // -----------------------------------------------------

            var dictionary =
                obj as IDictionary<string, object>;

            if (dictionary != null)
            {
                foreach (string name in names)
                {
                    foreach (var item in dictionary)
                    {
                        if (string.Equals(
                                item.Key,
                                name,
                                StringComparison.OrdinalIgnoreCase)
                            &&
                            item.Value != null)
                        {
                            string value =
                                item.Value.ToString();

                            if (!string.IsNullOrWhiteSpace(value))
                                return value;
                        }
                    }
                }

                return null;
            }

            // -----------------------------------------------------
            // Normal CLR object
            // -----------------------------------------------------

            Type type =
                obj.GetType();

            foreach (string name in names)
            {
                var property =
                    type.GetProperty(name);

                if (property == null)
                    continue;

                object value =
                    property.GetValue(
                        obj,
                        null);

                if (value == null)
                    continue;

                string stringValue =
                    value.ToString();

                if (!string.IsNullOrWhiteSpace(
                        stringValue))
                {
                    return stringValue;
                }
            }

            return null;
        }

        // =========================================================
        // Parse integer
        // =========================================================

        private static int ParseInt(
            string value)
        {
            int result;

            return int.TryParse(
                value,
                out result)
                ? result
                : 0;
        }

        // =========================================================
        // Format an expiry value as "MMM dd, yyyy" (e.g. Sep 03,
        // 2026) when it parses as a date; otherwise the raw value
        // is returned as-is; null/empty stays null so the caller
        // can omit the row entirely.
        // =========================================================

        private static string FormatExpiry(
            string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return null;

            DateTime parsed;

            if (DateTime.TryParse(
                    rawValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out parsed))
            {
                return parsed.ToString(
                    "MMM dd, yyyy",
                    CultureInfo.InvariantCulture);
            }

            return rawValue;
        }
    }
}