using System.Net;
using System.Net.Mail;

namespace TodoListApp.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string email, string subject, string message);
    }

    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public SmtpEmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            var smtpHost = _config["EmailSettings:Host"];
            var smtpPortString = _config["EmailSettings:Port"];
            var smtpUser = _config["EmailSettings:Username"];
            var smtpPass = _config["EmailSettings:Password"];

            // Always log to console for development visibility
            LogEmail(email, subject, message);

            // Check if SMTP is configured
            if (string.IsNullOrWhiteSpace(smtpHost) || 
                string.IsNullOrWhiteSpace(smtpUser) || 
                string.IsNullOrWhiteSpace(smtpPass)) 
            {
                Console.WriteLine("--- SMTP CONFIGURATION MISSING ---");
                return;
            }

            if (!int.TryParse(smtpPortString, out int smtpPort))
            {
                smtpPort = 587;
            }

            try
            {
                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtpUser),
                    Subject = subject,
                    Body = message,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("--- SMTP ERROR ---");
                Console.WriteLine($"Error sending email to {email}: {ex.Message}");
                Console.WriteLine("------------------");
            }
        }

        private void LogEmail(string email, string subject, string message)
        {
            Console.WriteLine();
            Console.WriteLine("==========================================");
            Console.WriteLine("📩 NEW EMAIL ALERT (DEVELOPMENT LOG)");
            Console.WriteLine($"TO: {email}");
            Console.WriteLine($"SUBJECT: {subject}");
            Console.WriteLine("------------------------------------------");
            // Simple text version of the HTML message
            var textBody = message
                .Replace("<h3>", "").Replace("</h3>", "\n")
                .Replace("<p>", "").Replace("</p>", "\n")
                .Replace("<a href='", "Link: ")
                .Replace("'>", "\nText: ")
                .Replace("</a>", "");
            Console.WriteLine(textBody);
            Console.WriteLine("==========================================");
            Console.WriteLine();
            Console.Out.Flush();
        }
    }
}
