using System.Collections.Concurrent;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FindMyCat.Api.Validation;

internal sealed class RequestValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    private static readonly ConcurrentDictionary<Type, Type> ValidatorInterfaces = new();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values.OfType<object>())
        {
            if (services.GetService(ValidatorInterfaceFor(argument)) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }

        await next();
    }

    private static Type ValidatorInterfaceFor(object argument) =>
        ValidatorInterfaces.GetOrAdd(argument.GetType(), static argumentType => typeof(IValidator<>).MakeGenericType(argumentType));
}
