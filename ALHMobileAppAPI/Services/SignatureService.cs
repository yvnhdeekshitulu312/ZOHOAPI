using ALHMobileAppAPI.ALHAppDAL;
using ALHMobileAppAPI.CommonUtilities;
using ALHMobileAppAPI.Models;
using CommanUtilities.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
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

            string docName =
                string.IsNullOrWhiteSpace(
                    sig.RequestDocumentName)
                    ? "a document"
                    : sig.RequestDocumentName;

            // -----------------------------------------------------
            // Extract recipients
            // -----------------------------------------------------

            var targets =
                recips
                    .Select(r => new RecipientEmailTarget
                    {
                        Email = GetProp(
                            r,
                            "Email",
                            "EmailId",
                            "EmailAddress"),

                        Name = GetProp(
                            r,
                            "ReciepientName",
                            "RecipientName",
                            "Name",
                            "FullName"),

                        Order = ParseInt(
                            GetProp(
                                r,
                                "SigningOrder",
                                "SendingOrder",
                                "Order",
                                "SigningorderId"))
                    })
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(
                            x.Email))
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

            // -----------------------------------------------------
            // Send emails
            //
            // IMPORTANT:
            // Each email gets its own SMTP connection.
            // -----------------------------------------------------

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
                        baseUrl);

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
        // Email body
        // =========================================================

        private static string BuildEmailBody(
            string name,
            string docName,
            string baseUrl)
        {
            string greeting =
                string.IsNullOrWhiteSpace(name)
                    ? "Hello,"
                    : "Dear " + name + ",";

            string link =
                string.IsNullOrEmpty(baseUrl)
                    ? string.Empty
                    :
                    "<p style=\"margin:22px 0;\">" +
                    "<a href=\"" +
                    HttpUtility.HtmlAttributeEncode(
                        baseUrl +
                        "/dashboard/pendingdocuments") +
                    "\" " +
                    "style=\"" +
                    "background:#1855A4;" +
                    "color:#FFFFFF;" +
                    "text-decoration:none;" +
                    "padding:11px 20px;" +
                    "border-radius:8px;" +
                    "font-family:'Noto Kufi Arabic',sans-serif;" +
                    "font-weight:700;" +
                    "display:inline-block;\">" +
                    "Open the document" +
                    "</a>" +
                    "</p>";

            return
                "<div " +
                "style=\"" +
                "font-family:'Noto Kufi Arabic',Arial,sans-serif;" +
                "color:#002654;" +
                "font-size:14px;" +
                "line-height:1.7;\">" +

                "<p>" +
                HttpUtility.HtmlEncode(greeting) +
                "</p>" +

                "<p>" +
                "You have a document waiting for your signature: " +
                "<b>" +
                HttpUtility.HtmlEncode(docName) +
                "</b>." +
                "</p>" +

                link +

                "<p " +
                "style=\"color:#969696;font-size:12px;\">" +
                "Al Hammadi Hospitals — Document Signing Portal" +
                "</p>" +

                "</div>";
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
    }
}