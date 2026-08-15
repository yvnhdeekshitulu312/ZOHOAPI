using System;
using System.Configuration;
using System.Security.Authentication;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

// NuGet:  Install-Package MailKit      (pulls in MimeKit)
//
// Microsoft 365 SMTP settings (Web.config <appSettings>):
//   Smtp.Host        = smtp.office365.com
//   Smtp.Port        = 587
//   Smtp.Username    = full mailbox address (e.g. no-reply@alhammadi.com)
//   Smtp.FromAddress = SAME mailbox you authenticate as
//
namespace ALHMobileAppAPI.CommonUtilities
{
    public static class EmailHelper
    {
        private static string Cfg(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        public static bool SendEmail(string toEmail, string toName, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) { return false; }

            string host     = Cfg("Smtp.Host") ?? "smtp.office365.com";
            int    port     = int.TryParse(Cfg("Smtp.Port"), out var p) ? p : 587;
            string fromName = Cfg("Smtp.FromName") ?? "Al Hammadi e-Signature";

            string user = Cfg("Smtp.Username") ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
            string pass = Cfg("Smtp.Password") ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD");

            string fromAddr = Cfg("Smtp.FromAddress");
            if (string.IsNullOrWhiteSpace(fromAddr)) { fromAddr = user; }

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(fromName, fromAddr));
            msg.To.Add(new MailboxAddress(string.IsNullOrWhiteSpace(toName) ? toEmail : toName, toEmail));
            msg.Subject = subject;
            msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using (var client = new SmtpClient())
            {
                // restrict to modern TLS (1.2, and 1.3 where the OS/.NET supports it)
                client.SslProtocols = SslProtocols.Tls12;
                try { client.SslProtocols |= (SslProtocols)12288; /* Tls13 if available */ } catch { }

                // EXPLICIT STARTTLS on 587 — this is the "assign STARTTLS to mail" step
                client.Connect(host, port, SecureSocketOptions.StartTls);

                // Basic auth (username must be the full email; see OAuth2 note below if this 535s)
                client.Authenticate(user, pass);

                client.Send(msg);
                client.Disconnect(true);
            }
            return true;
        }

        // ── If Basic auth STILL returns 535, the mailbox/tenant blocks it. ──
        // Switch AuthenticateAsync to OAuth2 (no password):
        //
        //   // 1) get an app-only token with MSAL (Microsoft.Identity.Client)
        //   var app = ConfidentialClientApplicationBuilder.Create(clientId)
        //       .WithClientSecret(clientSecret)
        //       .WithTenantId(tenantId).Build();
        //   var token = (await app.AcquireTokenForClient(
        //       new[] { "https://outlook.office365.com/.default" }).ExecuteAsync()).AccessToken;
        //
        //   // 2) authenticate with the token instead of a password
        //   var oauth2 = new SaslMechanismOAuth2(user, token);
        //   client.Connect(host, port, SecureSocketOptions.StartTls);
        //   client.Authenticate(oauth2);
        //
        // Azure AD app needs application permission "SMTP.SendAsApp" (admin-consented)
        // and the mailbox granted. Say the word and I'll write this out in full.
    }
}
