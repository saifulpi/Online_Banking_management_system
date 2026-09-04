using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace OnlineBankingSystem.Data;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
    bool IsConfigured { get; }
}

/// <summary>
/// Sends email via Gmail SMTP. Credentials are read from configuration /
/// environment variables (never hardcoded). Uses an App Password, not the
/// normal Gmail sign-in password.
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.Host) &&
        !string.IsNullOrWhiteSpace(_settings.Username) &&
        !string.IsNullOrWhiteSpace(_settings.AppPassword) &&
        !string.IsNullOrWhiteSpace(_settings.FromEmail);

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("SMTP is not configured; skipping email to {To}. Set EmailSettings via environment variables.", to);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, string.IsNullOrWhiteSpace(_settings.FromName) ? null : _settings.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            Credentials = new NetworkCredential(_settings.Username, _settings.AppPassword),
            EnableSsl = _settings.EnableSsl
        };

        await client.SendMailAsync(message);
    }
}