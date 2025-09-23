using Microsoft.Extensions.DependencyInjection;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Infrastructure.Serialization;

namespace PostTradeSystem.Infrastructure.Extensions;

public static class SerializationSetupExtensions
{
    public static IServiceCollection AddCompleteSerializationSetup(this IServiceCollection services)
    {
        services.AddSerializationManagement();
        services.AddInfrastructureSerialization();
        
        return services;
    }
    
    public static async Task EnsureSerializationInitializedAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        await scope.ServiceProvider.InitializeSerializationAsync();
    }
}