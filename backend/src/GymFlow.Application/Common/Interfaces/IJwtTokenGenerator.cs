using GymFlow.Domain.Entities;

namespace GymFlow.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    (string Token, DateTimeOffset ExpiresAt) Generate(User user);
}
