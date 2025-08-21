namespace Modern.CRDT.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Modern.CRDT.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJsonCrdt(this IServiceCollection services)
    {
        services.TryAddSingleton<IJsonCrdtPatcher, JsonCrdtPatcher>();
        services.TryAddSingleton<IJsonCrdtApplicator, JsonCrdtApplicator>();
        services.TryAddSingleton<IJsonCrdtService, JsonCrdtService>();

        return services;
    }
}