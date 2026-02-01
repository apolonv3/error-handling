namespace ErrorHandling.Domain.Results;

/// <summary>
/// Base type for domain errors; carries a code, message, type, and optional metadata.
/// </summary>
/// <remarks>
/// Used by <see cref="Result"/> and <see cref="Result{T}"/> to represent failure. Subtypes
/// (e.g. <see cref="OrderError"/>, <see cref="ValidationError"/>, <see cref="BusinessRuleError"/>)
/// add strongly-typed data and enable exhaustive pattern matching in the API layer.
/// </remarks>
public class Error : IEquatable<Error>
{
    private Dictionary<string, object>? _metadata;

    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }
    public IReadOnlyDictionary<string, object> Metadata => _metadata ?? new Dictionary<string, object>();

    public Error(string message)
        : this(null, message, ErrorType.Failure) { }

    public Error(string? code, string message, ErrorType type = ErrorType.Failure)
    {
        Code = code ?? "GENERAL_ERROR";
        Message = message ?? "An error occurred";
        Type = type;
    }

    public Error WithMetadata(string key, object value)
    {
        _metadata ??= new Dictionary<string, object>();
        _metadata[key] = value;
        return this;
    }

    public static Error NotFound(string entityName, object id) =>
        new Error("NOT_FOUND", $"{entityName} with id '{id}' was not found", ErrorType.NotFound);

    public static Error Validation(string message) =>
        new Error("VALIDATION_ERROR", message, ErrorType.Validation);

    public static Error Validation(string field, string message)
    {
        var error = new Error("VALIDATION_ERROR", message, ErrorType.Validation);
        error.WithMetadata("field", field);
        return error;
    }

    public static Error Conflict(string message) =>
        new Error("CONFLICT", message, ErrorType.Conflict);

    public static Error Unauthorized(string message = "Unauthorized") =>
        new Error("UNAUTHORIZED", message, ErrorType.Unauthorized);

    public static Error Forbidden(string message = "Forbidden") =>
        new Error("FORBIDDEN", message, ErrorType.Forbidden);

    public static Error Failure(string message) => new Error("FAILURE", message, ErrorType.Failure);

    public static Error Critical(string message) =>
        new Error("CRITICAL_ERROR", message, ErrorType.Critical);

    public bool Equals(Error? other)
    {
        if (other is null)
            return false;
        return Code == other.Code && Message == other.Message && Type == other.Type;
    }

    public override bool Equals(object? obj) => Equals(obj as Error);

    public override int GetHashCode() => HashCode.Combine(Code, Message, Type);

    public override string ToString() => $"[{Code}] {Message}";

    public static implicit operator string?(Error? error) => error?.ToString();
}

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    Critical = 6,
}

public class CompositeError : Error
{
    public IReadOnlyList<Error> Errors { get; }

    public CompositeError(params Error[] errors)
        : base("COMPOSITE_ERROR", "Multiple errors occurred")
    {
        Errors = errors?.ToList() ?? new List<Error>();
    }

    public override string ToString()
    {
        var errorMessages = string.Join("; ", Errors.Select(e => e.ToString()));
        return $"[{Code}] {Message}: {errorMessages}";
    }
}

public class ValidationError : Error
{
    public string Field { get; }
    public object? AttemptedValue { get; }

    public ValidationError(string field, string message, object? attemptedValue = null)
        : base("VALIDATION_ERROR", message, ErrorType.Validation)
    {
        Field = field;
        AttemptedValue = attemptedValue;
        WithMetadata("field", field);
        if (attemptedValue != null)
            WithMetadata("attemptedValue", attemptedValue);
    }
}

public class BusinessRuleError : Error
{
    public string RuleName { get; }

    public BusinessRuleError(string ruleName, string message)
        : base($"BUSINESS_RULE_{ruleName.ToUpperInvariant()}", message, ErrorType.Failure)
    {
        RuleName = ruleName;
        WithMetadata("ruleName", ruleName);
    }
}

public class InvalidStateTransitionError : Error
{
    public string FromState { get; }
    public string ToState { get; }
    public string EntityType { get; }

    public InvalidStateTransitionError(string fromState, string toState, string entityType)
        : base(
            "INVALID_STATE_TRANSITION",
            $"Cannot transition from '{fromState}' to '{toState}' for {entityType}",
            ErrorType.Failure
        )
    {
        FromState = fromState;
        ToState = toState;
        EntityType = entityType;
        WithMetadata("fromState", fromState);
        WithMetadata("toState", toState);
        WithMetadata("entityType", entityType);
    }
}
