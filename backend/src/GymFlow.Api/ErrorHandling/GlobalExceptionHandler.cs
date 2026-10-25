using GymFlow.Application.Common.Exceptions;
using GymFlow.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.ErrorHandling;

public class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, code, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "validation_failed", "One or more fields are invalid."),
            DomainException e => (StatusCodes.Status400BadRequest, e.Code, e.Message),
            NotFoundException e => (StatusCodes.Status404NotFound, e.Code, e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, e.Code, e.Message),
            UnauthorizedException e => (StatusCodes.Status401Unauthorized, e.Code, e.Message),
            ForbiddenException e => (StatusCodes.Status403Forbidden, e.Code, e.Message),
            _ => (StatusCodes.Status500InternalServerError, "server_error", "Something went wrong.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");

        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = code;

        if (exception is ValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors
                .GroupBy(e => JsonCamelCase(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    private static string JsonCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
