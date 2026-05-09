using AxiomFlow.Core.Contracts;
using AxiomFlow.Core.Models;
using System.Text.RegularExpressions;

namespace AxiomFlow.Samples.Insurance.Evaluators;

/// <summary>
/// A deterministic evaluator for the Insurance sample that checks discount percentages
/// using regex pattern matching. Does not require an LLM or external service.
/// Ideal for demos, testing, and CI/CD gates.
/// </summary>
public sealed partial class DiscountComplianceEvaluator : IAxiomEvaluator
{
    private const double MaxDiscountPercent = 15.0;

    public string Name => "DiscountCompliance";

    public Task<ValidationResult> EvaluateAsync(
        InvariantContext context,
        CancellationToken cancellationToken = default)
    {
        var fullText = $"{context.AgentThought} {context.AgentAction} {context.Observation}";
        var discounts = ExtractDiscountValues(fullText);

        if (discounts.Count == 0)
        {
            return Task.FromResult(new ValidationResult
            {
                Score = 1.0,
                Analysis = "No discount values detected in the agent's reasoning chain. Compliant.",
                EvaluatorName = Name,
                InvariantName = context.InvariantName
            });
        }

        var maxDiscount = discounts.Max();
        var passed = maxDiscount <= MaxDiscountPercent;
        var score = passed ? 1.0 : Math.Max(0.0, 1.0 - ((maxDiscount - MaxDiscountPercent) / 100.0) * 3);

        var analysis = passed
            ? $"All discount values are within the {MaxDiscountPercent}% threshold. " +
              $"Highest discount found: {maxDiscount}%. ✅ Compliant."
            : $"VIOLATION: Discount of {maxDiscount}% exceeds the maximum allowed {MaxDiscountPercent}%. " +
              $"This requires senior underwriter approval. Detected discounts: [{string.Join("%, ", discounts)}%].";

        return Task.FromResult(new ValidationResult
        {
            Score = Math.Clamp(score, 0.0, 1.0),
            Analysis = analysis,
            EvaluatorName = Name,
            InvariantName = context.InvariantName,
            Metadata = new Dictionary<string, object>
            {
                ["maxDiscount"] = maxDiscount,
                ["allDiscounts"] = discounts,
                ["threshold"] = MaxDiscountPercent
            }
        });
    }

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
        return discounts.Distinct().ToList();
    }

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*%", RegexOptions.IgnoreCase)]
    private static partial Regex DiscountPattern();
}
