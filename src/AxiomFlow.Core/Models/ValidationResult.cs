namespace AxiomFlow.Core.Models;

/// <summary>
/// Immutable result of a single evaluator's assessment of an agent output.
/// Score ranges from 0.0 (complete failure) to 1.0 (full compliance).
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// Compliance score: 0.0 = fail, 1.0 = pass.
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Human-readable explanation of the evaluation outcome.
    /// </summary>
    public required string Analysis { get; init; }

    /// <summary>
    /// Name of the evaluator that produced this result.
    /// </summary>
    public required string EvaluatorName { get; init; }

    /// <summary>
    /// The invariant that was being evaluated.
    /// </summary>
    public required string InvariantName { get; init; }

    /// <summary>
    /// Configurable threshold above which the result is considered passing.
    /// </summary>
    public double PassThreshold { get; init; } = 0.7;

    /// <summary>
    /// Whether this individual result passes the configured threshold.
    /// </summary>
    public bool Passed => Score >= PassThreshold;

    /// <summary>
    /// UTC timestamp of when the evaluation was performed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Extensible metadata bag for evaluator-specific data (e.g., search chunk IDs, LLM token counts).
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}
