using AxiomFlow.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AxiomFlow.Persistence.Extensions;

/// <summary>
/// DI registration extensions for AxiomFlow Cosmos DB persistence.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Cosmos DB Semantic Ledger provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Configuration delegate for Cosmos DB options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAxiomFlowCosmosLedger(
        this IServiceCollection services,
        Action<CosmosLedgerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<ISemanticLedger, CosmosLedgerProvider>();

        return services;
    }
}
