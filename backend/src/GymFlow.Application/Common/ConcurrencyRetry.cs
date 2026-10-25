using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Application.Common;

public static class ConcurrencyRetry
{
    public const int MaxAttempts = 8;

    public static async Task<T> WithConcurrencyRetryAsync<T>(this IApplicationDbContext db, Func<Task<T>> action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (attempt >= MaxAttempts)
                    throw new ConflictException("session_busy", "This session is very busy right now. Please try again.");

                db.ChangeTracker.Clear();
                await Task.Delay(Random.Shared.Next(5, 20 * attempt));
            }
        }
    }
}
