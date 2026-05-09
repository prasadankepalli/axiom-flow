using AxiomFlow.Core.Contracts;
using AxiomFlow.Core.Models;
using System.Text.RegularExpressions;

namespace AxiomFlow.Samples.Insurance.Invariants;

/// <summary>
/// Semantic invariant that ensures a "Premium Discount" never exceeds 15%
/// in an agent's reasoning chain.
/// 
/// This is a domain-specific business rule for commercial lines underwriting:
/// discounts above 15% require senior underwriter approval and must never
/// be auto-approved by an AI agent.
/// </summary>
public sealed partial class PremiumThresholdInvariant : ISemanticInvariant
{
    /// <summary>
    /// Maximum discount percentage allowed without human approval.
    /// </summary>
    private const double MaxDiscountPercent = 15.0;

    public string Name => "PremiumThreshold";

    public string Description =>
        $"Premium discounts must not exceed {MaxDiscountPercent}% in the agent's reasoning chain. " +
        $"Any discount above {MaxDiscountPercent}% requires senior underwriter approval.";

    public InvariantContext BuildContext(string agentThought, string agentAction, string observation)
    {
        return new InvariantContext
        {
            InvariantName = Name,
            InvariantDescription = Description,
            AgentThought = agentThought,
            AgentAction = agentAction,
            Observation = observation,
            FullResponse = $"{agentThought}\n{agentAction}\n{observation}",
            Properties = new Dictionary<string, object>
            {
                ["maxDiscountPercent"] = MaxDiscountPercent,
                ["extractedDiscounts"] = ExtractDiscountValues($"{agentThought} {agentAction} {observation}")
            }
        };
    }

    public bool ShouldApply(string agentAction)
    {
        // Apply this invariant when the agent is making pricing/discount decisions
        var action = agentAction.ToLowerInvariant();
        return action.Contains("discount") ||
               action.Contains("premium") ||
               action.Contains("pricing") ||
               action.Contains("rate") ||
               action.Contains("agent_response"); // Always apply to final responses
    }

    /// <summary>
    /// Extracts numeric discount values from text using regex.
    /// Looks for patterns like "10% discount", "discount of 20%", "15 percent".
    /// </summary>
    private static List<double> ExtractDiscountValues(string text)
    {
        var discounts = new List<double>();

        var matches = DiscountPattern().Matches(text);
        foreach (Match match in matches)
        {
            if (double.TryParse(match.Groups[1].Value, out var value))
            {
                discounts.Add(value);
            }
        }

        return discounts;
    }

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*%?\s*(?:percent|%)\s*(?:discount|off|reduction)", RegexOptions.IgnoreCase)]
    private static partial Regex DiscountPattern();
}
