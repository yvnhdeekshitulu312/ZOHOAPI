using ALHMobileAppAPI.ALHAppDAL;
using ALHMobileAppAPI.CommonUtilities;
using ALHMobileAppAPI.Models;
using CommanUtilities.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;

namespace ALHMobileAppAPI.Services
{
    public class SignatureService
    {
        public LoginDetails ValidateLoginCredentials(string username, string password)
        {
            SignatureDAL dal = new SignatureDAL();
            LoginDetails obj = dal.ValidateLoginCredentials(username, password);
            return obj;
        }

        public Base SaveSignatureRequests(SignatureModel SigParams)
        {
            SignatureDAL dal = new SignatureDAL();
            var result = dal.SaveSignatureRequests(SigParams);

            // Fire signature-request emails only when the save succeeded.
            // Never let an email failure break the API response.
            try
            {
                if (result != null && result.Code == CommanUtilities.Models.ProcessStatus.Success)
                {
                    SendSignatureEmails(SigParams);
                }
            }
            catch (Exception ex)
            {
                HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(ex, "SignatureService", "Error sending signature emails", "");
            }

            return result;
        }

        public Base FetchSignatureRequests(string RequestId)
        {
            SignatureDAL dal = new SignatureDAL();
            return dal.FetchSignatureRequests(RequestId);
        }

        public Base FetchSSSignatureReciepientUsers(string name)
        {
            SignatureDAL dal = new SignatureDAL();
            return dal.FetchSSSignatureReciepientUsers(name);
        }

        // ─────────────────────────────────────────────────────────────
        //  Email notifications (SMTP via EmailHelper / Web.config)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Emails recipients of a signature request. When SendInOrder is true,
        /// only the first signer (lowest signing order) is notified; the rest
        /// are emailed as each prior signer completes (call ResendNext there).
        /// </summary>
        private void SendSignatureEmails(SignatureModel sig)
        {
            if (sig == null || sig.ReciepientsXML == null) { return; }

            var recips = sig.ReciepientsXML.Cast<object>().ToList();
            if (recips.Count == 0) { return; }

            string baseUrl = ConfigurationManager.AppSettings["EsignAppBaseUrl"] ?? string.Empty;
            string docName = string.IsNullOrWhiteSpace(sig.RequestDocumentName) ? "a document" : sig.RequestDocumentName;

            // Extract (email, name, order) from each recipient object regardless of
            // the exact property names your recipient model uses.
            var targets = recips
                .Select(r => new
                {
                    Email = GetProp(r, "Email", "EmailId", "EmailAddress"),
                    Name  = GetProp(r, "ReciepientName", "RecipientName", "Name", "FullName"),
                    Order = ParseInt(GetProp(r, "SigningOrder", "SendingOrder", "Order", "SigningorderId"))
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Email))
                .ToList();

            if (targets.Count == 0) { return; }

            // send-in-order? notify only the first signer; else notify everyone
            var toNotify = sig.SendInOrder
                ? targets.OrderBy(t => t.Order == 0 ? int.MaxValue : t.Order).Take(1).ToList()
                : targets;

            foreach (var t in toNotify)
            {
                string subject = "Signature requested: " + docName;
                string body = BuildEmailBody(t.Name, docName, baseUrl);
                EmailHelper.SendEmail(t.Email, t.Name, subject, body);
            }
        }

        private static string BuildEmailBody(string name, string docName, string baseUrl)
        {
            string greeting = string.IsNullOrWhiteSpace(name) ? "Hello," : ("Dear " + name + ",");
            string link = string.IsNullOrEmpty(baseUrl)
                ? string.Empty
                : "<p style=\"margin:22px 0;\"><a href=\"" + baseUrl + "/dashboard/pendingdocuments\" " +
                  "style=\"background:#1855A4;color:#FFFFFF;text-decoration:none;padding:11px 20px;border-radius:8px;" +
                  "font-family:'Noto Kufi Arabic',sans-serif;font-weight:700;display:inline-block;\">Open the document</a></p>";

            return
                "<div style=\"font-family:'Noto Kufi Arabic',Arial,sans-serif;color:#002654;font-size:14px;line-height:1.7;\">" +
                    "<p>" + System.Web.HttpUtility.HtmlEncode(greeting) + "</p>" +
                    "<p>You have a document waiting for your signature: <b>" +
                        System.Web.HttpUtility.HtmlEncode(docName) + "</b>.</p>" +
                    link +
                    "<p style=\"color:#969696;font-size:12px;\">Al Hammadi Hospitals — Document Signing Portal</p>" +
                "</div>";
        }

        /// <summary>Reads the first matching property (as string) from an object via reflection.</summary>
        /// <summary>
        /// Reads the first matching property (as string) from a recipient item,
        /// whether it deserialized into a typed object, a Newtonsoft JObject, or
        /// an IDictionary. Loosely-typed ReciepientsXML arrives as JObject, so a
        /// plain reflection GetProperty("Email") returns null — hence targets = 0.
        /// </summary>
        private static string GetProp(object obj, params string[] names)
        {
            if (obj == null) { return null; }

            // Newtonsoft JObject (the usual case for object/dynamic-typed ReciepientsXML)
            JObject jo = obj as JObject;
            if (jo != null)
            {
                foreach (string n in names)
                {
                    JToken tok;
                    if (jo.TryGetValue(n, StringComparison.OrdinalIgnoreCase, out tok)
                        && tok != null && tok.Type != JTokenType.Null)
                    {
                        string val = tok.ToString();
                        if (!string.IsNullOrWhiteSpace(val)) { return val; }
                    }
                }
                return null;
            }

            // IDictionary<string, object> (ExpandoObject / some model binders)
            var dict = obj as System.Collections.Generic.IDictionary<string, object>;
            if (dict != null)
            {
                foreach (string n in names)
                {
                    foreach (var kv in dict)
                    {
                        if (string.Equals(kv.Key, n, StringComparison.OrdinalIgnoreCase) && kv.Value != null)
                        {
                            string val = kv.Value.ToString();
                            if (!string.IsNullOrWhiteSpace(val)) { return val; }
                        }
                    }
                }
                return null;
            }

            // Typed CLR object (reflection)
            Type type = obj.GetType();
            foreach (string n in names)
            {
                var pi = type.GetProperty(n);
                if (pi != null)
                {
                    object v = pi.GetValue(obj, null);
                    if (v != null)
                    {
                        string val = v.ToString();
                        if (!string.IsNullOrWhiteSpace(val)) { return val; }
                    }
                }
            }
            return null;
        }

        private static int ParseInt(string s)
        {
            int v;
            return int.TryParse(s, out v) ? v : 0;
        }
    }
}