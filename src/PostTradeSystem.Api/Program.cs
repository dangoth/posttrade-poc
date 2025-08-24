using Serilog;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Infrastructure.Schemas;
using PostTradeSystem.Infrastructure.Kafka;
using PostTradeSystem.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Schema Registry services
builder.Services.AddSingleton<ISchemaRegistry, InMemorySchemaRegistry>();
builder.Services.AddSingleton<KafkaSchemaRegistry>();

// Add Kafka services
builder.Services.AddSingleton<KafkaProducerService>();
builder.Services.AddHostedService<KafkaConsumerService>();
builder.Services.AddHostedService<SchemaRegistryInitializer>();

// Add Trade services
builder.Services.AddScoped<PostTradeSystem.Api.Services.TradeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithOpenApi();

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

// Map trade endpoints
app.MapTradeEndpoints();

app.Run();