using GymFlow.Application.Common;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Application.Members;

public class MemberService(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<MemberDto>> GetAsync(TenantRole? role, string? search, CancellationToken ct)
    {
        var query = db.TenantUsers.AsNoTracking();

        if (role is not null)
            query = query.Where(tu => tu.Role == role);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(tu =>
                tu.User!.FullName.ToLower().Contains(term) || tu.User.Email.Contains(term));
        }

        return await query
            .OrderByDescending(tu => tu.Role)
            .ThenBy(tu => tu.User!.FullName)
            .Select(tu => new MemberDto(
                tu.UserId, tu.User!.FullName, tu.User.Email, tu.User.PhoneNumber, tu.Role, tu.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<MemberDto> ChangeRoleAsync(Guid userId, TenantRole role, CancellationToken ct)
    {
        if (!Enum.IsDefined(role))
            throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure(nameof(role), "Role is not valid.")]);

        if (userId == currentUser.RequireUserId())
            throw new ForbiddenException("cannot_change_own_role", "You can't change your own role.");

        var membership = await FindAsync(userId, ct);
        membership.Role = role;
        await db.SaveChangesAsync(ct);

        return ToDto(membership);
    }

    public async Task RemoveAsync(Guid userId, CancellationToken ct)
    {
        if (userId == currentUser.RequireUserId())
            throw new ForbiddenException("cannot_remove_self", "You can't remove yourself from the gym.");

        var membership = await FindAsync(userId, ct);
        db.TenantUsers.Remove(membership);
        await db.SaveChangesAsync(ct);
    }

    private async Task<TenantUser> FindAsync(Guid userId, CancellationToken ct) =>
        await db.TenantUsers.Include(tu => tu.User).SingleOrDefaultAsync(tu => tu.UserId == userId, ct)
        ?? throw new NotFoundException("member_not_found", "Member not found in this gym.");

    private static MemberDto ToDto(TenantUser tu) =>
        new(tu.UserId, tu.User!.FullName, tu.User.Email, tu.User.PhoneNumber, tu.Role, tu.CreatedAt);
}
