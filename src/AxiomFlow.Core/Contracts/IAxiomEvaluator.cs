using AxiomFlow.Core.Models;

namespace AxiomFlow.Core.Contracts;

/// <summary>
/// Defines a semantic evaluator that assesses agent output against a specific dimension.
/// Implementations include AI-critic (LLM-based) and grounding (search-based) evaluators.
/// </summary>
public interface IAxiomEvaluator
{
    /// <summary>
    /// Unique name of this evaluator (e.g., "AICritic", "Grounding").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the given invariant context and returns a scored result.
    /// </summary>
    /// <param name="context">The runtime context containing the agent's TAO trace and business rule.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="ValidationResult"/> with a score (0-1) and analysis.</returns>
    Task<ValidationResult> EvaluateAsync(
        InvariantContext context,
        CancellationToken cancellationToken = default);
}
