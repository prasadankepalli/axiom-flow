using AxiomFlow.Core.Contracts;
using AxiomFlow.Core.Models;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;

namespace AxiomFlow.Evaluators;

/// <summary>
/// Compares agent output against Azure AI Search retrieval chunks to prevent hallucinations.
/// Measures how well the agent's response is grounded in the retrieved source documents.
/// 
/// Grounding score is computed as the ratio of agent claims that can be traced back
/// to at least one search result chunk.
/// </summary>
public sealed class GroundingEvaluator : IAxiomEvaluator
{
    private readonly SearchClient _searchClient;
    private readonly ILogger<GroundingEvaluator> _logger;

    public string Name => "Grounding";

    public GroundingEvaluator(SearchClient searchClient, ILogger<GroundingEvaluator> logger)
    {
        _searchClient = searchClient ?? throw new ArgumentNullException(nameof(searchClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ValidationResult> EvaluateAsync(
        InvariantContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[Grounding] Evaluating grounding for invariant '{Invariant}'", context.InvariantName);

        // Use provided grounding chunks or retrieve from search
        var groundingChunks = context.GroundingChunks;

        if (groundingChunks == null || groundingChunks.Count == 0)
        {
            groundingChunks = await RetrieveGroundingChunksAsync(context.AgentAction, cancellationToken);
        }

        if (groundingChunks.Count == 0)
        {
            _logger.LogWarning("[Grounding] No grounding chunks available for evaluation");
            return new ValidationResult
            {
                Score = 0.0,
                Analysis = "No grounding documents found. Unable to verify the agent's claims against source material.",
                EvaluatorName = Name,
                InvariantName = context.InvariantName
            };
        }

        // Compute grounding score
        var agentClaims = ExtractClaims(context.Observation);
        var (score, analysis) = ComputeGroundingScore(agentClaims, groundingChunks);

        _logger.LogInformation(
            "[Grounding] Invariant '{Invariant}' grounding score: {Score:F2} ({Grounded}/{Total} claims grounded)",
            context.InvariantName, score, agentClaims.Count(c => c.IsGrounded), agentClaims.Count);

        return new ValidationResult
        {
            Score = score,
            Analysis = analysis,
            EvaluatorName = Name,
            InvariantName = context.InvariantName,
            Metadata = new Dictionary<string, object>
            {
                ["chunkCount"] = groundingChunks.Count,
                ["claimCount"] = agentClaims.Count,
                ["groundedClaimCount"] = agentClaims.Count(c => c.IsGrounded)
            }
        };
    }

    private async Task<IReadOnlyList<string>> RetrieveGroundingChunksAsync(
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Size = 5,
                QueryType = SearchQueryType.Semantic
            };

            var response = await _searchClient.SearchAsync<SearchDocument>(
                query, searchOptions, cancellationToken);

            var chunks = new List<string>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                // Try common content field names
                if (result.Document.TryGetValue("content", out var content))
                {
                    chunks.Add(content?.ToString() ?? string.Empty);
                }
                else if (result.Document.TryGetValue("chunk", out var chunk))
                {
                    chunks.Add(chunk?.ToString() ?? string.Empty);
                }
            }

            return chunks.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Grounding] Failed to retrieve grounding chunks from search");
            return Array.Empty<string>();
        }
    }

    private static List<Claim> ExtractClaims(string text)
    {
        // Split the agent's observation into sentences as individual claims
        var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return sentences
            .Where(s => s.Length > 10) // Filter out trivially short fragments
            .Select(s => new Claim { Text = s, IsGrounded = false })
            .ToList();
    }

    private static (double Score, string Analysis) ComputeGroundingScore(
        List<Claim> claims,
        IReadOnlyList<string> chunks)
    {
        if (claims.Count == 0)
        {
            return (1.0, "No substantive claims found in agent output to verify.");
        }

        var combinedChunks = string.Join(" ", chunks).ToLowerInvariant();

        foreach (var claim in claims)
        {
            // Extract key terms from the claim and check for overlap
            var keyTerms = claim.Text
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 3) // Skip short words
                .ToList();

            if (keyTerms.Count == 0) continue;

            // A claim is considered grounded if a sufficient proportion of its key terms
            // appear in the source chunks
            var matchedTerms = keyTerms.Count(term => combinedChunks.Contains(term));
            var overlapRatio = (double)matchedTerms / keyTerms.Count;

            claim.IsGrounded = overlapRatio >= 0.5;
        }

        var groundedCount = claims.Count(c => c.IsGrounded);
        var score = (double)groundedCount / claims.Count;

        var ungroundedClaims = claims.Where(c => !c.IsGrounded).Select(c => c.Text).ToList();
        var analysis = score >= 0.7
            ? $"Agent response is well-grounded. {groundedCount}/{claims.Count} claims verified against source documents."
            : $"Agent response has grounding issues. {claims.Count - groundedCount}/{claims.Count} claims could not be " +
              $"verified. Ungrounded claims: {string.Join("; ", ungroundedClaims.Take(3))}";

        return (score, analysis);
    }

    private class Claim
    {
        public required string Text { get; init; }
        public bool IsGrounded { get; set; }
    }
}
