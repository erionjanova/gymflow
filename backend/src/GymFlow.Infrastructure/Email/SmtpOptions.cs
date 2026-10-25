namespace GymFlow.Infrastructure.Email;

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string? Username { get; set; }
    public string? Password { get; set; }

    public bool UseTls { get; set; }

    public string FromEmail { get; set; } = "no-reply@gymflow.local";
    public string FromName { get; set; } = "GymFlow";
}
