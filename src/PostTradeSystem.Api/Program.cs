using PostTradeSystem.Api.Endpoints;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Infrastructure.Extensions;
using PostTradeSystem.Infrastructure.Health;
using PostTradeSystem.Infrastructure.Kafka;
using PostTradeSystem.Infrastructure.Serialization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

ConfigureLogging(builder);
ConfigureWebHost(builder);
ConfigureServices(builder);

var app = builder.Build();

ConfigureMiddleware(app);
ConfigureEndpoints(app);

await app.RunAsync();

static void ConfigureLogging(WebApplicationBuilder builder)
{
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));
}

static void ConfigureWebHost(WebApplicationBuilder builder)
{
    var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:8080";
    builder.WebHost.UseUrls(urls);
}

static void ConfigureServices(WebApplicationBuilder builder)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddInfrastructureSerialization();

    AddKafkaServices(builder.Services);
    AddHealthServices(builder.Services);

    AddApplicationServices(builder.Services);
}

static void AddKafkaServices(IServiceCollection services)
{
    services.AddSingleton<KafkaHealthService>();
    services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
    services.AddHostedService<KafkaConsumerService>();
    services.AddHostedService<PostTradeSystem.Infrastructure.BackgroundServices.IdempotencyCleanupService>();
    services.AddHostedService<PostTradeSystem.Infrastructure.BackgroundServices.OutboxProcessorService>();
    services.AddHostedService<PostTradeSystem.Infrastructure.BackgroundServices.DeadLetterReprocessorService>();
}

static void AddHealthServices(IServiceCollection services)
{
    services.AddHealthChecks()
        .AddCheck<KafkaHealthCheck>("kafka");
}

static void AddApplicationServices(IServiceCollection services)
{
    services.AddScoped<PostTradeSystem.Api.Services.TradeService>();
}

static void ConfigureMiddleware(WebApplication app)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

static void ConfigureEndpoints(WebApplication app)
{
    app.MapHealthChecks("/health");

    ConfigureSchemaEndpoints(app);

    app.MapTradeEndpoints();
    app.MapOutboxAdminEndpoints();
}

static void ConfigureSchemaEndpoints(WebApplication app)
{
    app.MapGet("/schema/subjects", async (ISchemaRegistry schemaRegistry) =>
    {
        var subjects = await schemaRegistry.GetSubjectsAsync();
        return Results.Ok(subjects);
    })
    .WithName("GetSchemaSubjects")
    .WithOpenApi();

    app.MapGet("/schema/{subject}/latest", async (string subject, ISchemaRegistry schemaRegistry) =>
    {
        try
        {
            var schema = await schemaRegistry.GetLatestSchemaAsync(subject);
            return Results.Ok(schema);
        }
        catch (ArgumentException)
        {
            return Results.NotFound($"Schema not found for subject: {subject}");
        }
    })
    .WithName("GetLatestSchema")
    .WithOpenApi();
}

