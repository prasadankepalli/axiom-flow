using System.Diagnostics;
using AxiomFlow.Core.Contracts;
using AxiomFlow.Core.Models;
using Microsoft.Extensions.Logging;

namespace AxiomFlow.Core.Engine;

/// <summary>
/// Central orchestration engine that evaluates agent outputs against registered semantic invariants.
/// Iterates applicable invariants, fans out to evaluators, and aggregates results.
/// </summary>
public sealed class AxiomValidationEngine
{
    private readonly IEnumerable<IAxiomEvaluator> _evaluators;
    private readonly IEnumerable<ISemanticInvariant> _invariants;
    private readonly ILogger<AxiomValidationEngine> _logger;

    /// <summary>
    /// ActivitySource for OpenTelemetry distributed tracing.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("AxiomFlow.ValidationEngine", "1.0.0");

    public AxiomValidationEngine(
        IEnumerable<IAxiomEvaluator> evaluators,
        IEnumerable<ISemanticInvariant> invariants,
        ILogger<AxiomValidationEngine> logger)
    {
        _evaluators = evaluators ?? throw new ArgumentNullException(nameof(evaluators));
        _invariants = invariants ?? throw new ArgumentNullException(nameof(invariants));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates an agent's output against all applicable semantic invariants.
    /// </summary>
    /// <param name="agentThought">The agent's internal reasoning.</param>
    /// <param name="agentAction">The action the agent decided to take.</param>
    /// <param name="observation">The observation from the action.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated list of validation results from all evaluators and invariants.</returns>
    public async Task<IReadOnlyList<ValidationResult>> ValidateAsync(
        string agentThought,
        string agentAction,
        string observation,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AxiomFlow.Validate");
        activity?.SetTag("axiomflow.action", agentAction);

        var results = new List<ValidationResult>();
        var applicableInvariants = _invariants.Where(inv => inv.ShouldApply(agentAction)).ToList();

        _logger.LogInformation(
            "[AxiomFlow] Validating action '{Action}' against {Count} applicable invariants",
            agentAction, applicableInvariants.Count);

        activity?.SetTag("axiomflow.invariant_count", applicableInvariants.Count);

        foreach (var invariant in applicableInvariants)
        {
            var context = invariant.BuildContext(agentThought, agentAction, observation);

            using var invariantActivity = ActivitySource.StartActivity($"AxiomFlow.Invariant.{invariant.Name}");
            invariantActivity?.SetTag("axiomflow.invariant", invariant.Name);

            foreach (var evaluator in _evaluators)
            {
                using var evalActivity = ActivitySource.StartActivity($"AxiomFlow.Evaluate.{evaluator.Name}");

                try
                {
                    var sw = Stopwatch.StartNew();
                    var result = await evaluator.EvaluateAsync(context, cancellationToken);
                    sw.Stop();

                    _logger.LogInformation(
                        "[AxiomFlow] Evaluator '{Evaluator}' for invariant '{Invariant}': " +
                        "Score={Score:F2}, Passed={Passed}, Duration={Duration}ms",
                        evaluator.Name, invariant.Name, result.Score, result.Passed, sw.ElapsedMilliseconds);

                    evalActivity?.SetTag("axiomflow.evaluator", evaluator.Name);
                    evalActivity?.SetTag("axiomflow.score", result.Score);
                    evalActivity?.SetTag("axiomflow.passed", result.Passed);
                    evalActivity?.SetTag("axiomflow.duration_ms", sw.ElapsedMilliseconds);

                    results.Add(result);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[AxiomFlow] Evaluator '{Evaluator}' failed for invariant '{Invariant}'",
                        evaluator.Name, invariant.Name);

                    evalActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    // Evaluator failure is treated as a validation failure (fail-closed)
                    results.Add(new ValidationResult
                    {
                        Score = 0.0,
                        Analysis = $"Evaluator '{evaluator.Name}' threw an exception: {ex.Message}",
                        EvaluatorName = evaluator.Name,
                        InvariantName = invariant.Name,
                        Metadata = new Dictionary<string, object>
                        {
                            ["exceptionType"] = ex.GetType().Name,
                            ["failedOpen"] = false
                        }
                    });
                }
            }
        }

        var overallPass = results.Count == 0 || results.All(r => r.Passed);
        activity?.SetTag("axiomflow.overall_pass", overallPass);
        activity?.SetTag("axiomflow.result_count", results.Count);

        _logger.LogInformation(
            "[AxiomFlow] Validation complete. Results={Count}, OverallPass={Pass}",
            results.Count, overallPass);

        return results.AsReadOnly();
    }
}
