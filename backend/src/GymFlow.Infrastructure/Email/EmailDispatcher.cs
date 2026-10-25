using GymFlow.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GymFlow.Infrastructure.Email;

public class EmailDispatcher(
    EmailQueue queue,
    IOptions<SmtpOptions> options,
    ILogger<EmailDispatcher> logger) : BackgroundService
{
    private const int MaxAttempts = 3;
    private readonly SmtpOptions _smtp = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    await SendAsync(message, stoppingToken);
                    logger.LogInformation("Sent email '{Subject}' to {To}", message.Subject, message.To);
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (attempt == MaxAttempts)
                    {
                        logger.LogError(ex, "Giving up on email '{Subject}' to {To}", message.Subject, message.To);
                        break;
                    }

                    logger.LogWarning(ex, "Email attempt {Attempt} failed, retrying", attempt);
                    await Task.Delay(TimeSpan.FromSeconds(5 * attempt), stoppingToken);
                }
            }
        }
    }

    private async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromEmail));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_smtp.Host, _smtp.Port,
            _smtp.UseTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None, ct);

        if (!string.IsNullOrEmpty(_smtp.Username))
            await client.AuthenticateAsync(_smtp.Username, _smtp.Password ?? string.Empty, ct);

        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }
}
