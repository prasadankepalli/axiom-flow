using AxiomFlow.Core.Contracts;
using AxiomFlow.Core.Engine;
using AxiomFlow.Core.Middleware;
using AxiomFlow.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AxiomFlow.Core.Extensions;

/// <summary>
/// DI registration extensions for AxiomFlow.Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AxiomFlow validation engine, interceptor, and configures options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional configuration delegate for interceptor options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAxiomFlow(
        this IServiceCollection services,
        Action<AxiomInterceptorOptions>? configureOptions = null)
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<AxiomInterceptorOptions>(_ => { });
        }

        // Register core engine and middleware
        services.AddSingleton<AxiomValidationEngine>();
        services.AddSingleton<AxiomInterceptor>();

        return services;
    }

    /// <summary>
    /// Registers a semantic invariant with the DI container.
    /// </summary>
    /// <typeparam name="TInvariant">The invariant implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSemanticInvariant<TInvariant>(this IServiceCollection services)
        where TInvariant : class, ISemanticInvariant
    {
        services.AddSingleton<ISemanticInvariant, TInvariant>();
        return services;
    }

    /// <summary>
    /// Registers an axiom evaluator with the DI container.
    /// </summary>
    /// <typeparam name="TEvaluator">The evaluator implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAxiomEvaluator<TEvaluator>(this IServiceCollection services)
        where TEvaluator : class, IAxiomEvaluator
    {
        services.AddSingleton<IAxiomEvaluator, TEvaluator>();
        return services;
    }

    /// <summary>
    /// Registers dynamic invariants loaded from a JSON policy file (DSL).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="jsonFilePath">The absolute or relative path to the policies JSON file.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAxiomPolicies(this IServiceCollection services, string jsonFilePath)
    {
        if (!System.IO.File.Exists(jsonFilePath))
        {
            throw new System.IO.FileNotFoundException("Axiom policy file not found", jsonFilePath);
        }

        var json = System.IO.File.ReadAllText(jsonFilePath);
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var policies = System.Text.Json.JsonSerializer.Deserialize<List<AxiomPolicy>>(json, options);

        if (policies != null)
        {
            foreach (var policy in policies)
            {
                services.AddSingleton<ISemanticInvariant>(new DynamicSemanticInvariant(policy));
            }
        }

        return services;
    }
}
