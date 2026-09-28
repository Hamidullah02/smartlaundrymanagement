using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LaundryMVC.Services;

public class EmailSettings
{
    public SmtpSettings Smtp { get; set; } = new();
    public SenderInfo Sender { get; set; } = new();
}

public class SmtpSettings
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class SenderInfo
{
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "Smart Laundry";
}

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody);
}

public class MailKitEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(EmailSettings settings, ILogger<MailKitEmailSender> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.Sender.DisplayName, _settings.Sender.Email));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        // MailKit 4.x: pick the SecureSocketOptions explicitly. Port 587 = STARTTLS, port 465 = SSL-on-connect.
        var socketOption = _settings.Smtp.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        using var client = new SmtpClient();
        client.Timeout = 10000; // hard cap so a hung SMTP server can't stall the request forever
        try
        {
            await client.ConnectAsync(_settings.Smtp.Host, _settings.Smtp.Port, socketOption);
            await client.AuthenticateAsync(_settings.Smtp.Username, _settings.Smtp.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw;
        }
    }
}
