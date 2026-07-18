using ApiProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

using FluentValidation;

namespace AtomicArt.Api.ErrorHandling;

public static class ValidationProblemDetailsFactory
{
    private const string FieldsExtensionName = "fields";

    public static ApiProblemDetails Create(ValidationException validationException)
    {
        ArgumentNullException.ThrowIfNull(validationException);

        ApiProblemDetails problemDetails = new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Ошибка валидации запроса.",
            Detail = "Запрос не прошёл проверку."
        };
        string[] fields = validationException.Errors
            .Select(error => error.PropertyName)
            .Where(propertyName => !string.IsNullOrWhiteSpace(propertyName))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (fields.Length > 0)
        {
            problemDetails.Extensions[FieldsExtensionName] = fields;
        }

        return problemDetails;
    }
}
