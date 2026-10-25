using GymFlow.Application.Common;
using GymFlow.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymFlow.Application.Notifications;

public class SessionNotifier(IApplicationDbContext db, IEmailSender emailSender, IOptions<AppOptions> appOptions)
{
    public async Task NotifyAsync(SessionEmailKind kind, Guid sessionId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
            return;

        var session = await db.ClassSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new
            {
                ClassName = s.GymClass!.Name,
                s.StartsAt,
                GymName = s.Tenant!.Name,
                s.Tenant.Slug,
                s.Tenant.BrandColor,
                s.Tenant.TimeZone
            })
            .SingleOrDefaultAsync(ct);
        if (session is null)
            return;

        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Email, u.FullName, u.PreferredLanguage })
            .ToListAsync(ct);

        var localStart = TimeZoneInfo.ConvertTime(session.StartsAt, GymTime.FindTimeZone(session.TimeZone)).DateTime;
        var page = kind == SessionEmailKind.Cancelled ? "schedule" : "bookings";
        var url = $"{appOptions.Value.FrontendUrl.TrimEnd('/')}/g/{session.Slug}/{page}";

        foreach (var user in users)
        {
            var (subject, html) = EmailTemplates.Session(
                user.PreferredLanguage,
                kind,
                new SessionEmailModel(user.FullName, session.GymName, session.BrandColor, session.ClassName, localStart, url));
            await emailSender.QueueAsync(new EmailMessage(user.Email, subject, html), ct);
        }
    }
}
