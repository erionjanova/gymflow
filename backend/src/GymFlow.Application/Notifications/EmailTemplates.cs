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

public static class EmailTemplates
{
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
            ["role.Member"] = "anëtar"
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
            ["role.Member"] = "member"
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
