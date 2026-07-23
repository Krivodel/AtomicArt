using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AtomicArt.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DisableFormValueModelBindingAttribute
    : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.ValueProviderFactories
            .RemoveType<FormValueProviderFactory>();
        context.ValueProviderFactories
            .RemoveType<FormFileValueProviderFactory>();
        context.ValueProviderFactories
            .RemoveType<JQueryFormValueProviderFactory>();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }
}
