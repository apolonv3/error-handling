namespace ErrorHandling.Domain.Results;

public static class ResultExtensions
{
    // Async extensions
    public static async Task<Result<T>> ToResultAsync<T>(
        this Task<T> task,
        Func<Exception, Error>? errorHandler = null
    )
    {
        try
        {
            var value = await task;
            return Result<T>.Success(value);
        }
        catch (Exception ex)
        {
            var error = errorHandler?.Invoke(ex) ?? new Error("TASK_ERROR", ex.Message);
            return Result<T>.Failure(error);
        }
    }

    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Result<T> result,
        Func<T, Task<TNew>> mapper
    )
    {
        if (result.IsFailure)
            return Result<TNew>.Failure(result.Error!);

        var value = await mapper(result.Value);
        return Result<TNew>.Success(value);
    }

    public static async Task<Result<TNew>> BindAsync<T, TNew>(
        this Result<T> result,
        Func<T, Task<Result<TNew>>> binder
    )
    {
        if (result.IsFailure)
            return Result<TNew>.Failure(result.Error!);

        return await binder(result.Value);
    }

    public static async Task<Result<TNew>> BindAsync<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, Task<Result<TNew>>> binder
    )
    {
        var result = await resultTask;
        if (result.IsFailure)
            return Result<TNew>.Failure(result.Error!);

        return await binder(result.Value);
    }

    /// <summary>Async map for <see cref="Result{TValue, TError}"/>.</summary>
    public static async Task<Result<TNewValue, TError>> MapAsync<TValue, TNewValue, TError>(
        this Result<TValue, TError> result,
        Func<TValue, Task<TNewValue>> mapper
    )
        where TError : class
    {
        if (result.IsFailure)
            return Result<TNewValue, TError>.Failure(result.Error);

        var value = await mapper(result.Value);
        return Result<TNewValue, TError>.Success(value);
    }

    /// <summary>Async bind for <see cref="Result{TValue, TError}"/>.</summary>
    public static async Task<Result<TNewValue, TError>> BindAsync<TValue, TNewValue, TError>(
        this Result<TValue, TError> result,
        Func<TValue, Task<Result<TNewValue, TError>>> binder
    )
        where TError : class
    {
        if (result.IsFailure)
            return Result<TNewValue, TError>.Failure(result.Error);

        return await binder(result.Value);
    }

    /// <summary>Async bind for <see cref="Task"/> of <see cref="Result{TValue, TError}"/>.</summary>
    public static async Task<Result<TNewValue, TError>> BindAsync<TValue, TNewValue, TError>(
        this Task<Result<TValue, TError>> resultTask,
        Func<TValue, Task<Result<TNewValue, TError>>> binder
    )
        where TError : class
    {
        var result = await resultTask;
        if (result.IsFailure)
            return Result<TNewValue, TError>.Failure(result.Error);

        return await binder(result.Value);
    }

    public static async Task<Result<T>> TapAsync<T>(this Result<T> result, Func<T, Task> action)
    {
        if (result.IsSuccess)
            await action(result.Value);
        return result;
    }

    // Railway-oriented programming
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
    {
        if (result.IsFailure)
            return result;

        return predicate(result.Value) ? result : Result<T>.Failure(error);
    }

    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<T, bool> predicate,
        string errorMessage
    )
    {
        return result.Ensure(predicate, new Error(errorMessage));
    }

    public static Result<T> Unless<T>(this Result<T> result, Func<T, bool> predicate, Error error)
    {
        return result.Ensure(value => !predicate(value), error);
    }

    public static Result<(T1, T2)> Combine<T1, T2>(this Result<T1> result1, Result<T2> result2)
    {
        if (result1.IsFailure)
            return Result<(T1, T2)>.Failure(result1.Error!);
        if (result2.IsFailure)
            return Result<(T1, T2)>.Failure(result2.Error!);

        return Result<(T1, T2)>.Success((result1.Value, result2.Value));
    }

    public static Result<(T1, T2, T3)> Combine<T1, T2, T3>(
        this Result<T1> result1,
        Result<T2> result2,
        Result<T3> result3
    )
    {
        if (result1.IsFailure)
            return Result<(T1, T2, T3)>.Failure(result1.Error!);
        if (result2.IsFailure)
            return Result<(T1, T2, T3)>.Failure(result2.Error!);
        if (result3.IsFailure)
            return Result<(T1, T2, T3)>.Failure(result3.Error!);

        return Result<(T1, T2, T3)>.Success((result1.Value, result2.Value, result3.Value));
    }

    // Conversion
    public static Result<T> ToResult<T>(this T value)
    {
        return value != null
            ? Result<T>.Success(value)
            : Result<T>.Failure("NULL_VALUE", "Value cannot be null");
    }

    public static Result<T> ToResult<T>(this T? value)
        where T : struct
    {
        return value.HasValue
            ? Result<T>.Success(value.Value)
            : Result<T>.Failure("NULL_VALUE", "Value cannot be null");
    }

    // Error handling
    public static Result<T> OnFailure<T>(this Result<T> result, Action<Error> action)
    {
        if (result.IsFailure)
            action(result.Error!);
        return result;
    }

    public static Result<T> OnSuccess<T>(this Result<T> result, Action<T> action)
    {
        if (result.IsSuccess)
            action(result.Value);
        return result;
    }

    public static Result<T> Compensate<T>(this Result<T> result, Func<Error, Result<T>> compensator)
    {
        return result.IsFailure ? compensator(result.Error!) : result;
    }

    // Try pattern
    public static Result<T> Try<T>(Func<T> operation, Func<Exception, Error?>? errorHandler = null)
    {
        try
        {
            return Result<T>.Success(operation());
        }
        catch (Exception ex)
        {
            var error = errorHandler?.Invoke(ex) ?? new Error("EXCEPTION", ex.Message);
            return Result<T>.Failure(error);
        }
    }

    public static async Task<Result<T>> TryAsync<T>(
        Func<Task<T>> operation,
        Func<Exception, Error>? errorHandler = null
    )
    {
        try
        {
            var value = await operation();
            return Result<T>.Success(value);
        }
        catch (Exception ex)
        {
            var error = errorHandler?.Invoke(ex) ?? new Error("EXCEPTION", ex.Message);
            return Result<T>.Failure(error);
        }
    }
}
