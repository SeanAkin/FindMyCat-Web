using FindMyCat.Api.Errors;
using FindMyCat.Api.Json;
using FindMyCat.Api.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace FindMyCat.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddFindMyCatApi(this IServiceCollection services)
    {
        ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
            member is null
                ? null
                : ApiJsonOptions.Default.PropertyNamingPolicy?.ConvertName(member.Name) ?? member.Name;

        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        services.AddControllers(options => options.Filters.Add<RequestValidationFilter>())
            .AddJsonOptions(options => ApiJsonOptions.Configure(options.JsonSerializerOptions));

        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = DescribeRequestThatCouldNotBeBound);

        services.AddExceptionHandler<FindMyCatExceptionHandler>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }

    private static IActionResult DescribeRequestThatCouldNotBeBound(ActionContext context) =>
        new BadRequestObjectResult(new ApiError(Code: null, "The request could not be read."));
}
