using System.Text.Json.Serialization;

namespace AxiomFlow.Core.Models;

/// <summary>
/// A structured log entry that links a user prompt to its semantic validation status.
/// Persisted to the Semantic Ledger (Cosmos DB) for audit and observability.
/// </summary>
public sealed record ValidationArtifact
{
    /// <summary>
    /// Unique identifier for this artifact (Cosmos DB document ID).
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Session/conversation identifier. Used as the Cosmos DB partition key
    /// to co-locate all validations for a given agent session.
    /// </summary>
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    /// <summary>
    /// The original user prompt that triggered the agent execution.
    /// </summary>
    public required string UserPrompt { get; init; }

    /// <summary>
    /// The agent's response that was validated.
    /// </summary>
    public required string AgentResponse { get; init; }

    /// <summary>
    /// Name of the invariant that was evaluated.
    /// </summary>
    public required string InvariantName { get; init; }

    /// <summary>
    /// Individual evaluation results from all evaluators that ran.
    /// </summary>
    public required IReadOnlyList<ValidationResult> Results { get; init; }

    /// <summary>
    /// Whether all evaluators passed their configured thresholds.
    /// </summary>
    public bool OverallPass => Results.All(r => r.Passed);

    /// <summary>
    /// UTC timestamp of when the artifact was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Duration of the validation pipeline in milliseconds.
    /// </summary>
    public long ValidationDurationMs { get; init; }
}
