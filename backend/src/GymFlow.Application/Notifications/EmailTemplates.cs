using System.Globalization;
using System.Net;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Notifications;

public record InvitationEmailModel(
    string GymName,
    string BrandColor,
    string InviterName,
    TenantRole Role,
    string AcceptUrl,
    DateTimeOffset ExpiresAt);

public enum SessionEmailKind
{
    SpotOpened,
    Cancelled,
    Rescheduled
}

public record SessionEmailModel(
    string FullName,
    string GymName,
    string BrandColor,
    string ClassName,
    DateTime LocalStart,
    string Url);

public record PasswordResetEmailModel(string FullName, string ResetUrl, int ValidMinutes);

public static class EmailTemplates
{
    private const string PlatformName = "GymFlow";
    private const string PlatformColor = "#4d7c0f";

    private static readonly Dictionary<string, Dictionary<string, string>> Text = new()
    {
        ["sq"] = new()
        {
            ["invite.subject"] = "{0} ju fton të bashkoheni me {1}",
            ["invite.title"] = "Ftesë për t'u bashkuar",
            ["invite.intro"] = "{0} ju ftoi të bashkoheni me {1} si {2}.",
            ["invite.button"] = "Prano ftesën",
            ["invite.expires"] = "Kjo ftesë skadon më {0}. Nëse nuk e prisnit këtë email, mund ta injoroni.",
            ["role.Trainer"] = "trajner",
            ["role.Owner"] = "pronar",
            ["role.Member"] = "anëtar",
            ["session.SpotOpened.subject"] = "Keni një vend në {0}, {1}",
            ["session.SpotOpened.title"] = "Keni një vend!",
            ["session.SpotOpened.intro"] = "Përshëndetje {0}, u lirua një vend dhe kaluat nga lista e pritjes në {1}, {2}.",
            ["session.SpotOpened.button"] = "Shiko rezervimet",
            ["session.SpotOpened.footer"] = "Nëse nuk mund të vini, anuloni rezervimin që vendi t'i shkojë dikujt tjetër.",
            ["session.Cancelled.subject"] = "{0}, {1} u anulua",
            ["session.Cancelled.title"] = "Klasa u anulua",
            ["session.Cancelled.intro"] = "Përshëndetje {0}, {3} anuloi {1}, {2}. Rezervimi juaj u hoq automatikisht.",
            ["session.Cancelled.button"] = "Shiko orarin",
            ["session.Cancelled.footer"] = "Na vjen keq për ndryshimin.",
            ["session.Rescheduled.subject"] = "{0} u zhvendos në {1}",
            ["session.Rescheduled.title"] = "Ora e klasës ndryshoi",
            ["session.Rescheduled.intro"] = "Përshëndetje {0}, {3} zhvendosi {1}. Ora e re: {2}. Rezervimi juaj mbetet aktiv.",
            ["session.Rescheduled.button"] = "Shiko rezervimet",
            ["session.Rescheduled.footer"] = "Nëse ora e re nuk ju përshtatet, anuloni rezervimin që vendi t'i shkojë dikujt tjetër.",
            ["reset.subject"] = "Rivendosni fjalëkalimin tuaj në GymFlow",
            ["reset.title"] = "Rivendosni fjalëkalimin",
            ["reset.intro"] = "Përshëndetje {0}, morëm një kërkesë për të rivendosur fjalëkalimin e llogarisë suaj.",
            ["reset.button"] = "Zgjidhni një fjalëkalim të ri",
            ["reset.footer"] = "Linku vlen për {0} minuta dhe përdoret vetëm një herë. Nëse nuk e kërkuat ju, injorojeni këtë email: fjalëkalimi juaj mbetet i njëjtë."
        },
        ["en"] = new()
        {
            ["invite.subject"] = "{0} invited you to join {1}",
            ["invite.title"] = "You're invited",
            ["invite.intro"] = "{0} invited you to join {1} as a {2}.",
            ["invite.button"] = "Accept invitation",
            ["invite.expires"] = "This invitation expires on {0}. If you weren't expecting it, you can ignore this email.",
            ["role.Trainer"] = "trainer",
            ["role.Owner"] = "owner",
            ["role.Member"] = "member",
            ["session.SpotOpened.subject"] = "You're in: {0}, {1}",
            ["session.SpotOpened.title"] = "You're in!",
            ["session.SpotOpened.intro"] = "Hi {0}, a spot opened up and you moved from the waitlist into {1}, {2}.",
            ["session.SpotOpened.button"] = "View my bookings",
            ["session.SpotOpened.footer"] = "If you can't make it, cancel the booking so someone else can take the spot.",
            ["session.Cancelled.subject"] = "{0}, {1} was cancelled",
            ["session.Cancelled.title"] = "Class cancelled",
            ["session.Cancelled.intro"] = "Hi {0}, {3} cancelled {1}, {2}. Your booking has been removed.",
            ["session.Cancelled.button"] = "See the schedule",
            ["session.Cancelled.footer"] = "Sorry for the change.",
            ["session.Rescheduled.subject"] = "{0} moved to {1}",
            ["session.Rescheduled.title"] = "Class time changed",
            ["session.Rescheduled.intro"] = "Hi {0}, {3} moved {1}. New time: {2}. Your booking is still active.",
            ["session.Rescheduled.button"] = "View my bookings",
            ["session.Rescheduled.footer"] = "If the new time doesn't work for you, cancel the booking so someone else can take the spot.",
            ["reset.subject"] = "Reset your GymFlow password",
            ["reset.title"] = "Reset your password",
            ["reset.intro"] = "Hi {0}, we received a request to reset the password for your account.",
            ["reset.button"] = "Choose a new password",
            ["reset.footer"] = "The link works for {0} minutes and can be used once. If you didn't ask for this, ignore this email: your password stays the same."
        }
    };

    public static (string Subject, string Html) Invitation(string lang, InvitationEmailModel m)
    {
        var role = T(lang, $"role.{m.Role}");
        var subject = string.Format(T(lang, "invite.subject"), m.InviterName, m.GymName);
        var html = Layout(
            m.GymName,
            m.BrandColor,
            T(lang, "invite.title"),
            string.Format(T(lang, "invite.intro"), m.InviterName, m.GymName, role),
            T(lang, "invite.button"),
            m.AcceptUrl,
            string.Format(T(lang, "invite.expires"), m.ExpiresAt.ToString("dd.MM.yyyy")));

        return (subject, html);
    }

    public static (string Subject, string Html) Session(string lang, SessionEmailKind kind, SessionEmailModel m)
    {
        var culture = CultureInfo.GetCultureInfo(lang == "sq" ? "sq-AL" : "en-GB");
        var when = m.LocalStart.ToString("dddd, d MMMM, HH:mm", culture);
        var key = $"session.{kind}";

        var subject = string.Format(T(lang, $"{key}.subject"), m.ClassName, when);
        var html = Layout(
            m.GymName,
            m.BrandColor,
            T(lang, $"{key}.title"),
            string.Format(T(lang, $"{key}.intro"), m.FullName, m.ClassName, when, m.GymName),
            T(lang, $"{key}.button"),
            m.Url,
            T(lang, $"{key}.footer"));

        return (subject, html);
    }

    public static (string Subject, string Html) PasswordReset(string lang, PasswordResetEmailModel m)
    {
        var html = Layout(
            PlatformName,
            PlatformColor,
            T(lang, "reset.title"),
            string.Format(T(lang, "reset.intro"), m.FullName),
            T(lang, "reset.button"),
            m.ResetUrl,
            string.Format(T(lang, "reset.footer"), m.ValidMinutes));

        return (T(lang, "reset.subject"), html);
    }

    private static string T(string lang, string key) =>
        Text.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var value) ? value : Text["en"][key];

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static string Layout(
        string gymName, string brandColor, string title, string intro, string buttonText, string buttonUrl, string footer) =>
        $"""
        <!doctype html>
        <html>
        <body style="margin:0;padding:0;background:#f4f4f5;font-family:Segoe UI,Helvetica,Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:32px 16px;">
            <tr><td align="center">
              <table width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#ffffff;border-radius:16px;overflow:hidden;">
                <tr><td style="background:{E(brandColor)};padding:24px 32px;">
                  <span style="color:#ffffff;font-size:20px;font-weight:700;">{E(gymName)}</span>
                </td></tr>
                <tr><td style="padding:32px;">
                  <h1 style="margin:0 0 8px;color:#18181b;font-size:22px;">{E(title)}</h1>
                  <p style="margin:0 0 24px;color:#3f3f46;font-size:15px;line-height:1.5;">{E(intro)}</p>
                  <a href="{E(buttonUrl)}" style="display:inline-block;background:{E(brandColor)};color:#ffffff;text-decoration:none;font-weight:600;font-size:14px;padding:12px 24px;border-radius:999px;">{E(buttonText)}</a>
                  <p style="margin:24px 0 0;color:#71717a;font-size:13px;line-height:1.5;">{E(footer)}</p>
                </td></tr>
                <tr><td style="padding:16px 32px;background:#fafafa;color:#a1a1aa;font-size:12px;">GymFlow</td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}
