using System.Diagnostics;
using ErrorHandling.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace ErrorHandling.Api.Middleware;

/// <summary>
/// Middleware that catches unhandled exceptions and converts them to RFC 7807 Problem Details.
/// </summary>
/// <remarks>
/// Wraps the pipeline in try/catch. Domain exceptions (e.g. <see cref="EntityNotFoundException"/>,
/// <see cref="ValidationException"/>, <see cref="BusinessRuleException"/>) are mapped to
/// appropriate HTTP status and problem details; other exceptions return 500 and optionally
/// include details in development.
/// </remarks>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment
    )
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An error occurred: {Message}", exception.Message);

        Microsoft.AspNetCore.Mvc.ProblemDetails problemDetails = exception switch
        {
            EntityNotFoundException notFound => CreateProblemDetails(
                context,
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                "https://example.com/errors/not-found",
                notFound.Message,
                notFound
            ),

            AggregateNotFoundException aggregateNotFound => CreateProblemDetails(
                context,
                StatusCodes.Status404NotFound,
                "Aggregate Not Found",
                "https://example.com/errors/aggregate-not-found",
                aggregateNotFound.Message,
                aggregateNotFound
            ),

            BusinessRuleException businessRule => CreateProblemDetails(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "Business Rule Violation",
                $"https://example.com/errors/{businessRule.Code.ToLowerInvariant()}",
                businessRule.Message,
                businessRule
            ),

            InvariantViolationException invariant => CreateProblemDetails(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "Business Invariant Violation",
                $"https://example.com/errors/{invariant.Code.ToLowerInvariant()}",
                invariant.Message,
                invariant
            ),

            InvalidStateTransitionException stateTransition => CreateProblemDetails(
                context,
                StatusCodes.Status422UnprocessableEntity,
                "Invalid State Transition",
                "https://example.com/errors/invalid-state-transition",
                stateTransition.Message,
                stateTransition
            ),

            ValidationException validation => CreateValidationProblemDetails(context, validation),

            DuplicateEntityException duplicate => CreateProblemDetails(
                context,
                StatusCodes.Status409Conflict,
                "Duplicate Entity",
                "https://example.com/errors/duplicate",
                duplicate.Message,
                duplicate
            ),

            ConcurrencyException concurrency => CreateProblemDetails(
                context,
                StatusCodes.Status409Conflict,
                "Concurrency Conflict",
                "https://example.com/errors/concurrency-conflict",
                concurrency.Message,
                concurrency
            ),

            UnauthorizedAccessException _ => CreateProblemDetails(
                context,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "https://example.com/errors/unauthorized",
                "You are not authorized to perform this action",
                null
            ),

            ArgumentNullException argNull => CreateProblemDetails(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid Argument",
                "https://example.com/errors/invalid-argument",
                $"Required argument was null: {argNull.ParamName}",
                null
            ),

            ArgumentException arg => CreateProblemDetails(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid Argument",
                "https://example.com/errors/invalid-argument",
                arg.Message,
                null
            ),

            InvalidOperationException invalid => CreateProblemDetails(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid Operation",
                "https://example.com/errors/invalid-operation",
                invalid.Message,
                null
            ),

            _ => CreateProblemDetails(
                context,
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "https://example.com/errors/internal-server-error",
                "An unexpected error occurred",
                null
            ),
        };

        context.Response.StatusCode =
            problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private Microsoft.AspNetCore.Mvc.ProblemDetails CreateProblemDetails(
        HttpContext context,
        int status,
        string title,
        string type,
        string detail,
        DomainException? domainException
    )
    {
        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = context.Request.Path,
        };

        // Add common extensions
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow;

        // Add domain exception specific data
        if (domainException != null)
        {
            problemDetails.Extensions["errorCode"] = domainException.Code;

            if (!string.IsNullOrEmpty(domainException.CorrelationId))
                problemDetails.Extensions["correlationId"] = domainException.CorrelationId;

            if (!string.IsNullOrEmpty(domainException.UserId))
                problemDetails.Extensions["userId"] = domainException.UserId;

            if (!string.IsNullOrEmpty(domainException.TenantId))
                problemDetails.Extensions["tenantId"] = domainException.TenantId;

            if (domainException.Extensions != null && domainException.Extensions.Count > 0)
            {
                foreach (var kvp in domainException.Extensions)
                {
                    problemDetails.Extensions[kvp.Key] = kvp.Value;
                }
            }
        }

        // Only include debugging information in development
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["machineName"] = Environment.MachineName;
            problemDetails.Extensions["environment"] = _environment.EnvironmentName;
        }

        return problemDetails;
    }

    private ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext context,
        ValidationException validation
    )
    {
        var problemDetails = new ValidationProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Type = "https://example.com/errors/validation",
            Detail = validation.Message,
            Instance = context.Request.Path,
        };

        // Add validation errors to the errors dictionary
        if (validation.ValidationErrors != null)
        {
            foreach (var error in validation.ValidationErrors)
            {
                if (!problemDetails.Errors.ContainsKey(error.Field))
                    problemDetails.Errors[error.Field] = new[] { error.Message };
                else
                {
                    var current = problemDetails.Errors[error.Field].ToList();
                    current.Add(error.Message);
                    problemDetails.Errors[error.Field] = current.ToArray();
                }
            }
        }

        // Add common extensions
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow;
        problemDetails.Extensions["errorCode"] = validation.Code;

        if (!_environment.IsProduction())
        {
            problemDetails.Extensions["environment"] = _environment.EnvironmentName;
        }

        return problemDetails;
    }
}
