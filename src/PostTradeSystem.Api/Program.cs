using Microsoft.EntityFrameworkCore;
using PostTradeSystem.Api.Endpoints;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization;
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

await ConfigureApplicationAsync(app);
ConfigureMiddleware(app);
ConfigureEndpoints(app);

await RunApplicationAsync(app);

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
    services.AddSingleton<KafkaProducerService>();
    services.AddHostedService<KafkaConsumerService>();
    services.AddHostedService<PostTradeSystem.Infrastructure.BackgroundServices.IdempotencyCleanupService>();
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

static async Task ConfigureApplicationAsync(WebApplication app)
{
    if (app.Environment.IsDevelopment())
    {
        await ApplyDatabaseMigrationsAsync(app);
        await InitializeSchemasAsync(app);
    }
}

static async Task ApplyDatabaseMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var migrationLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<PostTradeSystem.Infrastructure.Data.PostTradeDbContext>();
        await context.Database.MigrateAsync();
        migrationLogger.LogInformation("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        migrationLogger.LogError(ex, "Failed to apply database migrations: {Message}", ex.Message);
        throw;
    }
}

static async Task InitializeSchemasAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var schemaLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        await scope.ServiceProvider.InitializeSerializationAsync();
        schemaLogger.LogInformation("Serialization and schema initialization completed successfully");
    }
    catch (Exception ex)
    {
        schemaLogger.LogError(ex, "Failed to initialize serialization and schemas: {Message}", ex.Message);
    }
}

static void ConfigureMiddleware(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Starting PostTrade System API in {Environment} environment", app.Environment.EnvironmentName);

    app.UseSwagger();
    app.UseSwaggerUI();
}

static void ConfigureEndpoints(WebApplication app)
{
    app.MapHealthChecks("/health");

    ConfigureSchemaEndpoints(app);

    app.MapTradeEndpoints();
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

static async Task RunApplicationAsync(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    
    try
    {
        await app.StartAsync();
        logger.LogInformation("PostTrade System API started successfully on {Urls}", string.Join(", ", app.Urls));
        
        await app.WaitForShutdownAsync();
        logger.LogInformation("Application shutdown completed");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Application failed to start: {Message}", ex.Message);
        throw;
    }
}