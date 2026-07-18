using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;

using AtomicArt.Application;
using AtomicArt.Api.Authentication;
using AtomicArt.Api.Filters;
using AtomicArt.Api.Middleware;
using AtomicArt.Api.ModelMetadata;
using AtomicArt.Api.ErrorHandling;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain;
using AtomicArt.Infrastructure;
using AtomicArt.Infrastructure.Generation;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<RequiredBodyActionFilter>();
});
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = ProviderCredentialAuthenticationDefaults.SchemeName;
        options.DefaultChallengeScheme = ProviderCredentialAuthenticationDefaults.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, ProviderCredentialAuthenticationHandler>(
        ProviderCredentialAuthenticationDefaults.SchemeName,
        _ => { });
builder.Services.AddAuthorization();
builder.Services.AddDomainServices();
string modelMetadataPath = GenerationModelCatalogDefaults.ResolvePath(builder.Environment.ContentRootPath);
TestGenerationOptions testGenerationOptions = builder.Configuration
    .GetSection(TestGenerationOptions.SectionName)
    .Get<TestGenerationOptions>()
    ?? new TestGenerationOptions();
GenerationModelCatalogDto modelCatalog;

try
{
    modelCatalog = JsonModelMetadataStartupLoader.Load(
        modelMetadataPath,
        new FileGenerationModelCatalogJsonSource());
    modelCatalog = TestGenerationModelCatalogAugmenter.AddTestModelIfEnabled(
        modelCatalog,
        testGenerationOptions);
}
catch (InvalidOperationException exception)
{
    LogStartupException(builder, exception);

    throw;
}

builder.Services.AddSingleton(modelCatalog);
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

WebApplication app = builder.Build();

app.Logger.LogInformation(
    "API is ready to start with {ModelCount} generation models. Test generation enabled: {TestGenerationEnabled}.",
    modelCatalog.Models.Count,
    testGenerationOptions.Enabled);

app.UseMiddleware<ApiRequestLoggingMiddleware>();

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        IExceptionHandlerPathFeature? exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        Exception? exception = exceptionFeature?.Error;
        ProblemDetails problemDetails = CreateProblemDetails(exception);

        LogException(context, exception);

        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response
            .WriteAsJsonAsync(problemDetails, context.RequestAborted)
            .ConfigureAwait(false);
    });
});

app.UseMiddleware<ValidationExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();

static ProblemDetails CreateProblemDetails(Exception? exception)
{
    if (exception is ValidationException validationException)
    {
        return ValidationProblemDetailsFactory.Create(validationException);
    }

    return new ProblemDetails
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = "Внутренняя ошибка сервера.",
        Detail = "Не удалось обработать запрос."
    };
}

static void LogException(HttpContext context, Exception? exception)
{
    if (exception is null)
    {
        return;
    }

    ILogger<Program> logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    if (exception is ValidationException)
    {
        logger.LogWarning(
            exception,
            "HTTP request {TraceIdentifier} with method {Method} failed validation.",
            context.TraceIdentifier,
            context.Request.Method);

        return;
    }

    logger.LogError(
        exception,
        "HTTP request {TraceIdentifier} with method {Method} failed with an unhandled exception.",
        context.TraceIdentifier,
        context.Request.Method);
}

static void LogStartupException(
    WebApplicationBuilder builder,
    Exception exception)
{
    using ILoggerFactory loggerFactory = LoggerFactory.Create(logging =>
    {
        logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        logging.AddConsole();
        logging.AddDebug();
    });
    ILogger<Program> logger = loggerFactory.CreateLogger<Program>();

    logger.LogCritical(
        exception,
        "API startup failed while loading generation model metadata.");
}

public partial class Program
{
}
