using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Application.Classes;

public class ClassService(IApplicationDbContext db, IValidator<UpsertClassRequest> validator)
{
    public async Task<IReadOnlyList<ClassDto>> GetAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.GymClasses.AsNoTracking();
        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        var classes = await query.OrderBy(c => c.Name).ToListAsync(ct);
        return classes.Select(ClassDto.From).ToList();
    }

    public async Task<ClassDto> CreateAsync(UpsertClassRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var gymClass = new GymClass();
        Apply(gymClass, request);
        db.GymClasses.Add(gymClass);
        await db.SaveChangesAsync(ct);

        return ClassDto.From(gymClass);
    }

    public async Task<ClassDto> UpdateAsync(Guid id, UpsertClassRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var gymClass = await FindAsync(id, ct);
        Apply(gymClass, request);
        await db.SaveChangesAsync(ct);

        return ClassDto.From(gymClass);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var gymClass = await FindAsync(id, ct);

        if (await db.ClassSessions.AnyAsync(s => s.GymClassId == id, ct))
            gymClass.IsActive = false;
        else
            db.GymClasses.Remove(gymClass);

        await db.SaveChangesAsync(ct);
    }

    private async Task<GymClass> FindAsync(Guid id, CancellationToken ct) =>
        await db.GymClasses.SingleOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("class_not_found", "Class not found.");

    private static void Apply(GymClass gymClass, UpsertClassRequest r)
    {
        gymClass.Name = r.Name.Trim();
        gymClass.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        gymClass.DurationMinutes = r.DurationMinutes;
        gymClass.DefaultCapacity = r.DefaultCapacity;
        gymClass.IsActive = r.IsActive;
    }
}
