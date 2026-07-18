using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AtomicArt.Api.Filters;

public sealed class RequiredBodyActionFilter : IActionFilter
{
    private readonly ILogger<RequiredBodyActionFilter> _logger;

    public RequiredBodyActionFilter(ILogger<RequiredBodyActionFilter> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (ControllerParameterDescriptor parameter in GetBodyParameters(context))
        {
            if (!context.ActionArguments.TryGetValue(parameter.Name, out object? value) || value is null)
            {
                _logger.LogWarning(
                    "HTTP request was rejected because the required body is missing for action {ActionName}.",
                    context.ActionDescriptor.DisplayName);

                context.Result = CreateRequiredBodyResponse();
                return;
            }
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    private static IEnumerable<ControllerParameterDescriptor> GetBodyParameters(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor controllerActionDescriptor)
        {
            return [];
        }

        return controllerActionDescriptor.Parameters
            .OfType<ControllerParameterDescriptor>()
            .Where(IsBodyParameter);
    }

    private static bool IsBodyParameter(ControllerParameterDescriptor parameter)
    {
        return parameter.BindingInfo?.BindingSource == BindingSource.Body
            || parameter.ParameterInfo.GetCustomAttributes(typeof(FromBodyAttribute), false).Length > 0;
    }

    private static IActionResult CreateRequiredBodyResponse()
    {
        ProblemDetails problemDetails = new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Некорректный запрос.",
            Detail = "Тело запроса обязательно."
        };

        return new BadRequestObjectResult(problemDetails);
    }
}
