using System.Reflection;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Application.Tenancy;
using GymFlow.Domain.Common;
using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GymFlow.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
    : DbContext(options), IApplicationDbContext
{
    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(ApplicationDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public DbSet<User> Users => Set<User>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();

    private Guid CurrentTenantId => tenantContext.TenantId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType) && entityType.BaseType is null)
                ApplyTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantOwned =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenant();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
        {
            throw pg.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation when pg.ConstraintName == "ix_users_email" =>
                    new ConflictException("email_taken", "An account with this email already exists."),
                PostgresErrorCodes.UniqueViolation when pg.ConstraintName == "ix_tenants_slug" =>
                    new ConflictException("slug_taken", "This gym address is already taken."),
                PostgresErrorCodes.UniqueViolation when pg.ConstraintName == "ix_tenant_users_tenant_id_user_id" =>
                    new ConflictException("already_member", "This user is already part of the gym."),
                _ => ex
            };
        }
    }

    private void StampTenant()
    {
        var currentTenantId = tenantContext.TenantId;

        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = currentTenantId
                    ?? throw new InvalidOperationException(
                        $"Cannot save a new {entry.Entity.GetType().Name} without a gym for this request.");
            }

            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && currentTenantId is not null
                && entry.Entity.TenantId != currentTenantId)
            {
                throw new InvalidOperationException(
                    $"Blocked a write to {entry.Entity.GetType().Name} that belongs to another gym.");
            }

            if (entry.State == EntityState.Modified && entry.Property(e => e.TenantId).IsModified)
                throw new InvalidOperationException("The gym of an existing record can't be changed.");
        }
    }
}
