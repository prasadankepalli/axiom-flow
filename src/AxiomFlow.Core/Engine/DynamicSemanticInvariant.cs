using AxiomFlow.Core.Contracts;
using AxiomFlow.Core.Models;
using System.Text.RegularExpressions;

namespace AxiomFlow.Core.Engine;

/// <summary>
/// A semantic invariant that is driven by a data policy (DSL).
/// Allows for runtime-configurable business rules without code changes.
/// </summary>
public sealed class DynamicSemanticInvariant : ISemanticInvariant
{
    private readonly AxiomPolicy _policy;
    private readonly List<Regex> _actionFilters;

    public DynamicSemanticInvariant(AxiomPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        
        // Compile wildcards into regexes for efficient matching
        _actionFilters = _policy.ApplyToActions
            .Select(a => new Regex("^" + Regex.Escape(a).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase))
            .ToList();
    }

    public string Name => _policy.Name;
    public string Description => _policy.Description;

    public InvariantContext BuildContext(string agentThought, string agentAction, string observation)
    {
        return new InvariantContext
        {
            InvariantName = Name,
            InvariantDescription = Description,
            AgentThought = agentThought,
            AgentAction = agentAction,
            Observation = observation,
            Properties = _policy.Metadata?.ToDictionary(k => k.Key, v => (object)v.Value)
        };
    }

    public bool ShouldApply(string agentAction)
    {
        if (string.IsNullOrEmpty(agentAction)) return false;
        return _actionFilters.Any(f => f.IsMatch(agentAction));
    }
}
