using System;
using System.Configuration;
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

            message.Body = new BodyBuilder
            {
                HtmlBody = htmlBody
            }.ToMessageBody();

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