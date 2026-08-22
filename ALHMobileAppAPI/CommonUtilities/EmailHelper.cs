using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

using Microsoft.Identity.Client;

namespace ALHMobileAppAPI.CommonUtilities
{
    public static class EmailHelper
    {
        // =========================================================
        // Configuration
        // =========================================================

        private static string Cfg(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        // =========================================================
        // Inline logo
        //
        // Content-Id referenced by the branded email templates as
        // <img src="cid:ahhlogo" .../> (see SignatureService.cs and
        // EsignService.cs BuildEmailBody). Email clients cannot
        // reach a Windows file path directly, so the logo is
        // attached to every outgoing message as an inline (CID)
        // resource instead of linked by URL.
        //
        // The logo is read from an EMBEDDED RESOURCE baked into this
        // assembly, not an external file path -- this was previously
        // a hardcoded dev-machine path (D:\GIT\ZOHOAPI\...) that
        // didn't exist on the real server, so the logo silently
        // failed to attach (best-effort -- see the try/catch below)
        // and every branded email went out with a broken image.
        // Embedding it removes the server-path dependency entirely:
        // wherever this DLL is deployed, the logo goes with it.
        //
        // SETUP (one-time, in Visual Studio):
        //   1. Add AHH-Logo.png to the project, e.g. under
        //      CommonUtilities\EmbeddedResources\AHH-Logo.png
        //   2. Select it in Solution Explorer -> Properties ->
        //      Build Action = "Embedded Resource"
        //   3. Its resource name becomes:
        //      <DefaultNamespace>.CommonUtilities.EmbeddedResources.AHH-Logo.png
        //      (dots replace folder separators). Adjust
        //      LogoResourceName below to match your project's actual
        //      default namespace / folder if it differs.
        // =========================================================

        public const string LogoContentId = "ahhlogo";

        private const string LogoResourceName =
            "ALHMobileAppAPI.CommonUtilities.EmbeddedResources.AHH-Logo.png";

        // =========================================================
        // MSAL Application
        //
        // IMPORTANT:
        // This is intentionally created only once.
        // MSAL maintains its token cache internally.
        // =========================================================

        private static readonly Lazy<IConfidentialClientApplication> MsalApp =
            new Lazy<IConfidentialClientApplication>(() =>
            {
                string clientId = Cfg("AzureAd.ClientId");
                string clientSecret = Cfg("AzureAd.ClientSecret");
                string tenantId = Cfg("AzureAd.TenantId");

                if (string.IsNullOrWhiteSpace(clientId))
                    throw new ConfigurationErrorsException(
                        "AzureAd.ClientId is missing.");

                if (string.IsNullOrWhiteSpace(clientSecret))
                    throw new ConfigurationErrorsException(
                        "AzureAd.ClientSecret is missing.");

                if (string.IsNullOrWhiteSpace(tenantId))
                    throw new ConfigurationErrorsException(
                        "AzureAd.TenantId is missing.");

                return ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithAuthority(
                        "https://login.microsoftonline.com/" + tenantId)
                    .Build();
            });

        // =========================================================
        // Get OAuth token
        // =========================================================

        private static async Task<string> GetAccessTokenAsync(
            CancellationToken cancellationToken)
        {
            var result = await MsalApp.Value
                .AcquireTokenForClient(
                    new[]
                    {
                        "https://outlook.office365.com/.default"
                    })
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);

            if (result == null ||
                string.IsNullOrWhiteSpace(result.AccessToken))
            {
                throw new InvalidOperationException(
                    "Microsoft Entra ID returned an empty access token.");
            }

            return result.AccessToken;
        }

        // =========================================================
        // Public async method
        // =========================================================

        public static async Task<bool> SendEmailAsync(
            string toEmail,
            string toName,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                return false;

            // -----------------------------------------------------
            // Configuration
            // -----------------------------------------------------

            string host =
                Cfg("Smtp.Host") ??
                "smtp.office365.com";

            int port =
                int.TryParse(
                    Cfg("Smtp.Port"),
                    out int configuredPort)
                    ? configuredPort
                    : 587;

            string fromName =
                Cfg("Smtp.FromName") ??
                "Al Hammadi e-Signature";

            string fromAddress =
                Cfg("Smtp.FromAddress");

            if (string.IsNullOrWhiteSpace(fromAddress))
            {
                throw new ConfigurationErrorsException(
                    "Smtp.FromAddress is missing.");
            }

            // -----------------------------------------------------
            // Create message
            // -----------------------------------------------------

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    fromName,
                    fromAddress));

            message.To.Add(
                new MailboxAddress(
                    string.IsNullOrWhiteSpace(toName)
                        ? toEmail
                        : toName,
                    toEmail));

            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };

            // -----------------------------------------------------
            // Inline logo attachment (best-effort -- a missing or
            // unreadable embedded resource must never block the
            // email from sending; the template's <img> just renders
            // broken/shows its alt text in that case).
            // -----------------------------------------------------

            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                using (var resourceStream =
                    assembly.GetManifestResourceStream(LogoResourceName))
                {
                    if (resourceStream != null)
                    {
                        // MimeKit's LinkedResources.Add(name, stream) reads the
                        // stream fully before returning, so it's safe to let the
                        // `using` above dispose it right after this call.
                        var logo = bodyBuilder.LinkedResources.Add(
                            "AHH-Logo.png",
                            resourceStream);

                        logo.ContentId = LogoContentId;
                    }
                    else
                    {
                        // Wrong resource name is a setup mistake, not a runtime
                        // fluke -- log it so it doesn't silently ship broken.
                        // GetManifestResourceNames() lists what's actually
                        // embedded, useful for fixing LogoResourceName above.
                        HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(
                            new FileNotFoundException(
                                "Embedded logo resource not found: " + LogoResourceName +
                                ". Available resources: " +
                                string.Join(", ", assembly.GetManifestResourceNames())),
                            "EmailHelper",
                            "Could not attach inline logo",
                            "");
                    }
                }
            }
            catch (Exception ex)
            {
                HIS.TOOLS.Logger.ErrorLog.ErrorRoutine(
                    ex,
                    "EmailHelper",
                    "Could not attach inline logo",
                    "");
            }

            message.Body = bodyBuilder.ToMessageBody();

            // -----------------------------------------------------
            // Get OAuth token
            // -----------------------------------------------------

            string accessToken =
                await GetAccessTokenAsync(cancellationToken)
                    .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // -----------------------------------------------------
            // SMTP
            // -----------------------------------------------------

            using (var client = new SmtpClient())
            {
                // 30 second MailKit timeout
                client.Timeout = 30000;

                // TLS 1.2
                client.SslProtocols =
                    SslProtocols.Tls12;

                // -------------------------------------------------
                // Connect
                // -------------------------------------------------

                await client
                    .ConnectAsync(
                        host,
                        port,
                        SecureSocketOptions.StartTls,
                        cancellationToken)
                    .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                // -------------------------------------------------
                // OAuth2 / XOAUTH2
                // -------------------------------------------------

                var oauth2 =
                    new SaslMechanismOAuth2(
                        fromAddress,
                        accessToken);

                await client
                    .AuthenticateAsync(
                        oauth2,
                        cancellationToken)
                    .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                // -------------------------------------------------
                // Send
                // -------------------------------------------------

                await client
                    .SendAsync(
                        message,
                        cancellationToken)
                    .ConfigureAwait(false);

                // -------------------------------------------------
                // Disconnect
                // -------------------------------------------------

                if (client.IsConnected)
                {
                    await client
                        .DisconnectAsync(
                            true,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            return true;
        }
    }
}