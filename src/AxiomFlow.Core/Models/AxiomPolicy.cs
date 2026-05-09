namespace AxiomFlow.Core.Models;

/// <summary>
/// Represents a semantic invariant defined via policy (DSL) rather than code.
/// This allows business analysts to define rules in JSON/YAML.
/// </summary>
public sealed record AxiomPolicy
{
    /// <summary>
    /// Unique name of the invariant.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable description of the rule.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// List of agent actions this invariant should apply to.
    /// Supports wildcards (e.g., "apply_*" or "*").
    /// </summary>
    public required string[] ApplyToActions { get; init; }

    /// <summary>
    /// Optional metadata or custom configuration for the evaluators.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}
