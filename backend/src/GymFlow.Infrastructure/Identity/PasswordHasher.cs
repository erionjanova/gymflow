using GymFlow.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using IPasswordHasher = GymFlow.Application.Common.Interfaces.IPasswordHasher;

namespace GymFlow.Infrastructure.Identity;

public class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string hashedPassword, string providedPassword) =>
        _hasher.VerifyHashedPassword(null!, hashedPassword, providedPassword) != PasswordVerificationResult.Failed;
}
