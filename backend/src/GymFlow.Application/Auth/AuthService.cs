using GymFlow.Application.Common;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Application.Auth;

public class AuthService(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    ICurrentUser currentUser,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<UpdateProfileRequest> profileValidator,
    IValidator<ChangePasswordRequest> passwordValidator)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        await registerValidator.ValidateAndThrowAsync(request, ct);

        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("email_taken", "An account with this email already exists.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = TrimToNull(request.PhoneNumber),
            PreferredLanguage = request.PreferredLanguage
        };
        user.PasswordHash = passwordHasher.Hash(request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);

        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !passwordHasher.Verify(user.PasswordHash, request.Password))
            throw new UnauthorizedException("invalid_credentials", "Email or password is incorrect.");

        return CreateResponse(user);
    }

    public async Task<UserDto> GetCurrentAsync(CancellationToken ct)
    {
        var user = await GetCurrentUserEntityAsync(ct);
        return UserDto.From(user);
    }

    public async Task<UserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        await profileValidator.ValidateAndThrowAsync(request, ct);

        var user = await GetCurrentUserEntityAsync(ct);
        user.FullName = request.FullName.Trim();
        user.PhoneNumber = TrimToNull(request.PhoneNumber);
        user.PreferredLanguage = request.PreferredLanguage;
        await db.SaveChangesAsync(ct);

        return UserDto.From(user);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        await passwordValidator.ValidateAndThrowAsync(request, ct);

        var user = await GetCurrentUserEntityAsync(ct);
        if (!passwordHasher.Verify(user.PasswordHash, request.CurrentPassword))
            throw new DomainException("wrong_password", "The current password is incorrect.");

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MyGymDto>> GetMyGymsAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        return await db.TenantUsers.IgnoreQueryFilters().AsNoTracking()
            .Where(tu => tu.UserId == userId && !tu.Tenant!.IsSuspended)
            .OrderBy(tu => tu.Tenant!.Name)
            .Select(tu => new MyGymDto(tu.TenantId, tu.Tenant!.Name, tu.Tenant.Slug, tu.Tenant.BrandColor, tu.Role))
            .ToListAsync(ct);
    }

    private async Task<User> GetCurrentUserEntityAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        return await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedException("not_authenticated", "You need to log in.");
    }

    private AuthResponse CreateResponse(User user)
    {
        var (token, expiresAt) = tokenGenerator.Generate(user);
        return new AuthResponse(token, expiresAt, UserDto.From(user));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
