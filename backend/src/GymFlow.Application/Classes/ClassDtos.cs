using GymFlow.Domain.Entities;

namespace GymFlow.Application.Classes;

public record ClassDto(
    Guid Id,
    string Name,
    string? Description,
    int DurationMinutes,
    int DefaultCapacity,
    bool IsActive)
{
    public static ClassDto From(GymClass c) =>
        new(c.Id, c.Name, c.Description, c.DurationMinutes, c.DefaultCapacity, c.IsActive);
}

public record UpsertClassRequest(
    string Name,
    string? Description,
    int DurationMinutes,
    int DefaultCapacity,
    bool IsActive);
