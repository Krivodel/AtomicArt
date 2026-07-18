using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace AtomicArt.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IReadOnlyList<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);

        _validators = validators.ToList();
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        if (_validators.Count == 0)
        {
            return await next().ConfigureAwait(false);
        }

        ValidationContext<TRequest> context = new ValidationContext<TRequest>(request);
        ValidationResult[] validationResults = await Task.WhenAll(
                _validators.Select(validator => validator.ValidateAsync(context, ct)))
            .ConfigureAwait(false);
        List<ValidationFailure> failures = validationResults
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next().ConfigureAwait(false);
    }
}
