namespace ErrorHandling.Domain.Results;

/// <summary>
/// Represents the outcome of an operation that can succeed (unit) or fail with an <see cref="Error"/>.
/// </summary>
/// <remarks>
/// Use <see cref="Result.Success"/> and <see cref="Result.Failure(Error)"/> to create instances.
/// Use <see cref="Result.Combine"/> to aggregate multiple results. For a value on success, use
/// <see cref="Result{T}"/>.
/// </remarks>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error != null)
            throw new InvalidOperationException("Cannot have error on success result");
        if (!isSuccess && error == null)
            throw new InvalidOperationException("Must have error on failure result");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static Result Failure(string errorMessage) => new(false, new Error(errorMessage));

    public static Result Failure(string code, string message) =>
        new(false, new Error(code, message));

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    public static Result<T> Failure<T>(string errorMessage) => Result<T>.Failure(errorMessage);

    public static Result Combine(params Result[] results)
    {
        var failures = results.Where(r => r.IsFailure).ToList();
        if (failures.Any())
        {
            var errors = failures.Select(f => f.Error!).ToArray();
            return Failure(new CompositeError(errors));
        }
        return Success();
    }

    /// <summary>
    /// Handles success (unit) or failure with a typed result. Use instead of <see cref="Error"/> and null-forgiving.
    /// </summary>
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<Error, TResult> onFailure)
    {
        return IsSuccess ? onSuccess() : onFailure(Error!);
    }
}

/// <summary>
/// Represents the outcome of an operation that can succeed with a value of type <typeparamref name="T"/>
/// or fail with an <see cref="Error"/>.
/// </summary>
/// <remarks>
/// Use <see cref="Result{T}.Match"/> or <see cref="Result{T}.Match(Action{T}, Action{Error})"/> to
/// handle both cases. Use <see cref="ResultExtensions.MapAsync"/>, <see cref="ResultExtensions.BindAsync"/>
/// for railway-oriented composition.
/// </remarks>
/// <typeparam name="T">The type of the value on success.</typeparam>
public class Result<T> : Result
{
    private readonly T _value;

    public T Value
    {
        get
        {
            if (IsFailure)
                throw new InvalidOperationException("Cannot access value on failure result");
            return _value;
        }
    }

    protected Result(T value, bool isSuccess, Error? error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public static Result<T> Success(T value) => new(value, true, null!);

    public static new Result<T> Failure(Error error) => new(default!, false, error);

    public static new Result<T> Failure(string errorMessage) =>
        new(default!, false, new Error(errorMessage));

    public static new Result<T> Failure(string code, string message) =>
        new(default!, false, new Error(code, message));

    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess ? Result<TNew>.Success(mapper(Value)) : Result<TNew>.Failure(Error!);
    }

    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        return IsSuccess ? binder(Value) : Result<TNew>.Failure(Error!);
    }

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value) : onFailure(Error!);
    }

    public void Match(Action<T> onSuccess, Action<Error> onFailure)
    {
        if (IsSuccess)
            onSuccess(Value);
        else
            onFailure(Error!);
    }

    public Result<T> Tap(Action<T> action)
    {
        if (IsSuccess)
            action(Value);
        return this;
    }

    public Result<T> TapError(Action<Error> action)
    {
        if (IsFailure)
            action(Error!);
        return this;
    }

    public T? GetValueOrDefault(T? defaultValue = default)
    {
        return IsSuccess ? Value : defaultValue;
    }

    public static implicit operator Result<T>(T value)
    {
        return value != null
            ? Success(value)
            : Failure(new Error("NULL_VALUE", "Value cannot be null"));
    }
}

public class Result<TValue, TError>
    where TError : class
{
    private readonly TValue _value;
    private readonly TError _error;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public TValue Value
    {
        get
        {
            if (IsFailure)
                throw new InvalidOperationException("Cannot access value on failure result");
            return _value;
        }
    }

    public TError Error
    {
        get
        {
            if (IsSuccess)
                throw new InvalidOperationException("Cannot access error on success result");
            return _error;
        }
    }

    private Result(TValue value, TError error, bool isSuccess)
    {
        _value = value;
        _error = error;
        IsSuccess = isSuccess;
    }

    public static Result<TValue, TError> Success(TValue value) => new(value, default!, true);

    public static Result<TValue, TError> Failure(TError error) => new(default!, error, false);

    public Result<TNewValue, TError> Map<TNewValue>(Func<TValue, TNewValue> mapper)
    {
        return IsSuccess
            ? Result<TNewValue, TError>.Success(mapper(Value))
            : Result<TNewValue, TError>.Failure(Error);
    }

    public Result<TNewValue, TError> Bind<TNewValue>(Func<TValue, Result<TNewValue, TError>> binder)
    {
        return IsSuccess ? binder(Value) : Result<TNewValue, TError>.Failure(Error);
    }

    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<TError, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value) : onFailure(Error);
    }
}

/// <summary>
/// Represents the outcome of an operation that can succeed (unit) or fail with a typed error <typeparamref name="TError"/>.
/// </summary>
/// <remarks>
/// Use for operations that return no value on success but need a strongly-typed error (e.g. <see cref="OrderError"/>).
/// </remarks>
public class Result<TError>
    where TError : class
{
    private readonly TError _error;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public TError Error
    {
        get
        {
            if (IsSuccess)
                throw new InvalidOperationException("Cannot access error on success result");
            return _error;
        }
    }

    private Result(TError error, bool isSuccess)
    {
        _error = error;
        IsSuccess = isSuccess;
    }

    public static Result<TError> Success() => new(default!, true);

    public static Result<TError> Failure(TError error) => new(error, false);

    public TResult Match<TResult>(Func<TResult> onSuccess, Func<TError, TResult> onFailure)
    {
        return IsSuccess ? onSuccess() : onFailure(Error);
    }
}
