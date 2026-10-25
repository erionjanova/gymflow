namespace GymFlow.Application.Common.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsPlatformAdmin { get; }
}
