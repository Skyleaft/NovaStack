using FluentValidation;
using MediatR;
using NovaStack.SharedKernel.Results;

namespace Identity.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs FluentValidation before each command/query.
/// Returns a validation <see cref="Error"/> instead of throwing an exception.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0) return await next();

        var errorMessage = string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
        var error = NovaStack.SharedKernel.Results.Error.Validation("Validation.Failed", errorMessage);

        var failedResult = typeof(TResponse) == typeof(Result)
            ? (TResponse)(object)Result.Failure(error)
            : (TResponse)Activator.CreateInstance(
                typeof(TResponse),
                [null, false, error])!;

        return failedResult;
    }
}
