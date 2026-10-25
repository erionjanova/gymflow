namespace GymFlow.Application.Common.Interfaces;

public record EmailMessage(string To, string Subject, string HtmlBody);

public interface IEmailSender
{
    ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
