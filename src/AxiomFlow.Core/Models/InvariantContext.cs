namespace AxiomFlow.Core.Models;

/// <summary>
/// Runtime context envelope passed to evaluators during validation.
/// Encapsulates the agent's Thought-Action-Observation trace along with
/// the business rule being checked.
/// </summary>
public sealed record InvariantContext
{
    /// <summary>
    /// Name of the invariant being evaluated.
    /// </summary>
    public required string InvariantName { get; init; }

    /// <summary>
    /// Human-readable description of the business rule.
    /// </summary>
    public required string InvariantDescription { get; init; }

    /// <summary>
    /// The agent's internal reasoning (the "Thought" in the TAO loop).
    /// </summary>
    public required string AgentThought { get; init; }

    /// <summary>
    /// The action the agent decided to take.
    /// </summary>
    public required string AgentAction { get; init; }

    /// <summary>
    /// The observation / result from the action execution.
    /// </summary>
    public required string Observation { get; init; }

    /// <summary>
    /// The complete agent response text (for evaluators that need the full output).
    /// </summary>
    public string? FullResponse { get; init; }

    /// <summary>
    /// Optional retrieval chunks from Azure AI Search for grounding evaluation.
    /// </summary>
    public IReadOnlyList<string>? GroundingChunks { get; init; }

    /// <summary>
    /// Extensible properties bag for domain-specific context.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Properties { get; init; }
}
