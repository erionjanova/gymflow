using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GymFlow.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<TenantUser> TenantUsers { get; }
    DbSet<MembershipPlan> MembershipPlans { get; }
    DbSet<GymClass> GymClasses { get; }
    DbSet<ClassSession> ClassSessions { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<MemberSubscription> MemberSubscriptions { get; }

    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
