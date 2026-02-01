using System.Diagnostics;
using ErrorHandling.Domain.Results;
using Microsoft.AspNetCore.Mvc;

namespace ErrorHandling.Api.Extensions;

/// <summary>
/// Extension methods to convert domain <see cref="Result{T}"/> and <see cref="Error"/> into
/// HTTP responses using RFC 7807 Problem Details.
/// </summary>
/// <remarks>
/// Success results are mapped to 200/201/204 as appropriate. Failures are mapped using
/// type-safe pattern matching on <see cref="OrderError"/> (and <see cref="OrderErrorCode"/>)
/// when available, otherwise using <see cref="Error.Type"/> for non-order errors (e.g. from entities).
/// </remarks>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a <see cref="Result{T}"/> to an <see cref="IActionResult"/>.
    /// Success returns Ok(value); failure returns Problem Details with status and type derived from the error.
    /// </summary>
    public static IActionResult ToProblemDetails<T>(this Result<T> result, HttpContext context)
    {
        if (result.IsSuccess)
            return new OkObjectResult(result.Value);

        return ConvertErrorToProblemDetails(result.Error!, context);
    }

    /// <summary>
    /// Converts a unit <see cref="Result"/> to an <see cref="IActionResult"/>.
    /// Success returns 204 No Content; failure returns Problem Details.
    /// </summary>
    public static IActionResult ToProblemDetails(this Result result, HttpContext context)
    {
        if (result.IsSuccess)
            return new NoContentResult();

        return ConvertErrorToProblemDetails(result.Error!, context);
    }

    /// <summary>
    /// Converts a <see cref="Result{TValue, TError}"/> with <see cref="OrderError"/> to an <see cref="IActionResult"/>.
    /// Success returns Ok(value); failure returns Problem Details from the typed <see cref="OrderError"/>.
    /// </summary>
    public static IActionResult ToProblemDetails<T>(this Result<T, OrderError> result, HttpContext context)
    {
        if (result.IsSuccess)
            return new OkObjectResult(result.Value);

        return ConvertErrorToProblemDetails(result.Error, context);
    }

    /// <summary>
    /// Converts a unit <see cref="Result{OrderError}"/> to an <see cref="IActionResult"/>.
    /// Success returns 204 No Content; failure returns Problem Details from the typed <see cref="OrderError"/>.
    /// </summary>
    public static IActionResult ToProblemDetails(this Result<OrderError> result, HttpContext context)
    {
        if (result.IsSuccess)
            return new NoContentResult();

        return ConvertErrorToProblemDetails(result.Error, context);
    }

    /// <summary>
    /// Builds RFC 7807 Problem Details from a domain <see cref="Error"/> and current <see cref="HttpContext"/>.
    /// </summary>
    private static IActionResult ConvertErrorToProblemDetails(Error error, HttpContext context)
    {
        var (statusCode, type) = GetStatusAndType(error);

        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(error),
            Type = type,
            Detail = error.Message,
            Instance = context.Request.Path,
        };

        // Add error metadata
        problemDetails.Extensions["errorCode"] = error.Code;
        problemDetails.Extensions["errorType"] = error.Type.ToString();
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        problemDetails.Extensions["timestamp"] = System.DateTime.UtcNow;

        if (error.Metadata != null && error.Metadata.Count > 0)
        {
            foreach (var kvp in error.Metadata)
            {
                problemDetails.Extensions[kvp.Key] = kvp.Value;
            }
        }

        // Handle composite errors
        if (error is CompositeError composite)
        {
            var errors = new System.Collections.Generic.List<object>();
            foreach (var subError in composite.Errors)
            {
                errors.Add(
                    new
                    {
                        code = subError.Code,
                        message = subError.Message,
                        type = subError.Type.ToString(),
                        metadata = subError.Metadata,
                    }
                );
            }
            problemDetails.Extensions["errors"] = errors;
        }

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }

    /// <summary>
    /// Type-safe mapping: pattern match on OrderError for exhaustive handling.
    /// Falls back to Error.Type for non-OrderError (e.g. from entities).
    /// </summary>
    private static (int statusCode, string type) GetStatusAndType(Error error)
    {
        if (error is OrderError orderError)
        {
            return orderError.CodeEnum switch
            {
                OrderErrorCode.Validation => (
                    StatusCodes.Status400BadRequest,
                    "https://example.com/errors/validation"
                ),
                OrderErrorCode.EntityNotFound => (
                    StatusCodes.Status404NotFound,
                    "https://example.com/errors/not-found"
                ),
                OrderErrorCode.CustomerNotActive
                or OrderErrorCode.ProductInactive
                or OrderErrorCode.InsufficientStock
                or OrderErrorCode.InvalidStateTransition
                or OrderErrorCode.InsufficientPayment
                or OrderErrorCode.OrderMustHaveItems
                or OrderErrorCode.InsufficientCredit
                or OrderErrorCode.CreditOverflow => (
                    StatusCodes.Status422UnprocessableEntity,
                    "https://example.com/errors/business-rule"
                ),
                OrderErrorCode.NullValue => (
                    StatusCodes.Status400BadRequest,
                    "https://example.com/errors/validation"
                ),
                _ => (
                    // Unhandled OrderErrorCode: log so new codes get explicit mapping
                    StatusCodes.Status422UnprocessableEntity,
                    "https://example.com/errors/business-rule"
                ),
            };
        }

        return error.Type switch
        {
            ErrorType.Validation => (
                StatusCodes.Status400BadRequest,
                "https://example.com/errors/validation"
            ),
            ErrorType.NotFound => (
                StatusCodes.Status404NotFound,
                "https://example.com/errors/not-found"
            ),
            ErrorType.Conflict => (
                StatusCodes.Status409Conflict,
                "https://example.com/errors/conflict"
            ),
            ErrorType.Unauthorized => (
                StatusCodes.Status401Unauthorized,
                "https://example.com/errors/unauthorized"
            ),
            ErrorType.Forbidden => (
                StatusCodes.Status403Forbidden,
                "https://example.com/errors/forbidden"
            ),
            ErrorType.Critical => (
                StatusCodes.Status500InternalServerError,
                "https://example.com/errors/critical"
            ),
            _ => (
                StatusCodes.Status422UnprocessableEntity,
                "https://example.com/errors/business-rule"
            ),
        };
    }

    private static string GetTitle(Error error)
    {
        if (error is OrderError orderError)
        {
            return orderError.CodeEnum switch
            {
                OrderErrorCode.Validation => "Validation Error",
                OrderErrorCode.EntityNotFound => "Resource Not Found",
                OrderErrorCode.CustomerNotActive => "Customer Not Active",
                OrderErrorCode.ProductInactive => "Product Unavailable",
                OrderErrorCode.InsufficientStock => "Insufficient Stock",
                OrderErrorCode.InvalidStateTransition => "Invalid State Transition",
                OrderErrorCode.InsufficientPayment => "Insufficient Payment",
                OrderErrorCode.NullValue => "Validation Error",
                OrderErrorCode.OrderMustHaveItems => "Order Must Have Items",
                OrderErrorCode.InsufficientCredit => "Insufficient Credit",
                OrderErrorCode.CreditOverflow => "Credit Overflow",
                _ => "Business Rule Violation",
            };
        }

        return GetTitleFromType(error.Type);
    }

    private static string GetTitleFromType(ErrorType type) =>
        type switch
        {
            ErrorType.Validation => "Validation Error",
            ErrorType.NotFound => "Resource Not Found",
            ErrorType.Conflict => "Conflict",
            ErrorType.Unauthorized => "Unauthorized",
            ErrorType.Forbidden => "Forbidden",
            ErrorType.Critical => "Critical Error",
            _ => "Business Rule Violation",
        };
}
