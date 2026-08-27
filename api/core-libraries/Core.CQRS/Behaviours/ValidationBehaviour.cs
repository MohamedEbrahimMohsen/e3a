using Core.Localization;
using FluentValidation;
using MediatR;

namespace Core.CQRS.Behaviours;

public class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators, ILocalizer localizer) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            var validationResults = await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .Where(r => r.Errors.Any())
                .SelectMany(r => r.Errors)
                .ToList();

            var errorMessages = new List<string>();
            var errorCodes = new List<string>();

            foreach (var error in failures) {
                errorMessages.Add(localizer.GetMessage(error.ErrorCode, error.ErrorMessage));
                if(!string.IsNullOrEmpty(error.ErrorCode))
                    errorCodes.Add(error.ErrorCode);
            }

            if (failures.Count != 0)
            {
                throw new ValidationBehaviourException(errorCodes: errorCodes, errorMessages: errorMessages);
            }
        }
        return await next(cancellationToken);
    }
}