using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PostTradeSystem.Infrastructure.Data;

namespace PostTradeSystem.Infrastructure.Services;

public class DatabaseMigrationService : IHostedService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<DatabaseMigrationService> _logger;
    private readonly IHostEnvironment _environment;

    public DatabaseMigrationService(IServiceScopeFactory serviceScopeFactory, ILogger<DatabaseMigrationService> logger, IHostEnvironment environment)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _environment = environment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("Applying database migrations...");
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PostTradeDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Database migrations completed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}