namespace AxiomFlow.Core.Models;

/// <summary>
/// Represents a single step in the agent's Thought-Action-Observation loop.
/// Used to reconstruct the reasoning chain for semantic validation.
/// </summary>
public sealed record SemanticTrace
{
    /// <summary>
    /// Sequential step number in the reasoning chain.
    /// </summary>
    public required int StepNumber { get; init; }

    /// <summary>
    /// The agent's internal reasoning for this step.
    /// </summary>
    public required string Thought { get; init; }

    /// <summary>
    /// The action the agent decided to take (e.g., tool call, API invocation).
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// The result or observation from executing the action.
    /// </summary>
    public string? Observation { get; init; }

    /// <summary>
    /// UTC timestamp of when this step occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Duration of this step in milliseconds.
    /// </summary>
    public long DurationMs { get; init; }
}
