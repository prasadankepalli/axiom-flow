using AxiomFlow.Core.Models;

namespace AxiomFlow.Core.Contracts;

/// <summary>
/// Defines a business rule (semantic invariant) that must hold true across an agent's reasoning chain.
/// Invariants define WHAT to check; evaluators define HOW to check.
/// This separation enables a many-to-many relationship between rules and evaluation strategies.
/// </summary>
public interface ISemanticInvariant
{
    /// <summary>
    /// Unique name of this invariant (e.g., "PremiumThreshold", "CoverageLimit").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of the business rule this invariant enforces.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Builds the evaluation context from the agent's Thought-Action-Observation trace.
    /// </summary>
    /// <param name="agentThought">The agent's internal reasoning.</param>
    /// <param name="agentAction">The action the agent decided to take.</param>
    /// <param name="observation">The result from executing the action.</param>
    /// <returns>An <see cref="InvariantContext"/> ready for evaluator consumption.</returns>
    InvariantContext BuildContext(string agentThought, string agentAction, string observation);

    /// <summary>
    /// Determines whether this invariant should be applied to the given agent action.
    /// Allows filtering so that only relevant invariants run for each step.
    /// </summary>
    /// <param name="agentAction">The action the agent is about to take.</param>
    /// <returns>True if this invariant should be evaluated for the given action.</returns>
    bool ShouldApply(string agentAction);
}
