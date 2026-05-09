using AxiomFlow.Core.Contracts;
using AxiomFlow.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AxiomFlow.Evaluators;

/// <summary>
/// Uses a lightweight LLM (via IChatClient / Gemini 1.5 Flash) to compare
/// an agent's reasoning against business rules defined in the invariant.
/// 
/// The evaluator constructs a structured critique prompt that asks the LLM
/// to score compliance on a 0-1 scale with a detailed analysis.
/// </summary>
public sealed class AICriticEvaluator : IAxiomEvaluator
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<AICriticEvaluator> _logger;

    public string Name => "AICritic";

    public AICriticEvaluator(IChatClient chatClient, ILogger<AICriticEvaluator> logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ValidationResult> EvaluateAsync(
        InvariantContext context,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(context);

        _logger.LogDebug("[AICritic] Evaluating invariant '{Invariant}'", context.InvariantName);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        var responseText = response.Text ?? string.Empty;

        // Parse the structured response
        var (score, analysis) = ParseCritiqueResponse(responseText);

        _logger.LogInformation(
            "[AICritic] Invariant '{Invariant}' scored {Score:F2}",
            context.InvariantName, score);

        return new ValidationResult
        {
            Score = score,
            Analysis = analysis,
            EvaluatorName = Name,
            InvariantName = context.InvariantName,
            Metadata = new Dictionary<string, object>
            {
                ["rawResponse"] = responseText
            }
        };
    }

    private static string BuildSystemPrompt() =>
        """
        You are a semantic compliance auditor for an AI agent pipeline.
        Your role is to evaluate whether an agent's reasoning and actions comply with a specific business rule.
        
        Respond ONLY in the following JSON format:
        {
          "score": <number between 0.0 and 1.0>,
          "analysis": "<detailed explanation of compliance or violation>"
        }
        
        Scoring guide:
        - 1.0: Full compliance, no issues found
        - 0.7-0.9: Minor concerns but generally compliant
        - 0.4-0.6: Significant concerns, potential violation
        - 0.0-0.3: Clear violation of the business rule
        
        Be precise, cite specific evidence from the agent's reasoning, and explain your score.
        """;

    private static string BuildUserPrompt(InvariantContext context) =>
        $"""
        ## Business Rule (Invariant)
        **Name:** {context.InvariantName}
        **Description:** {context.InvariantDescription}
        
        ## Agent's Reasoning
        **Thought:** {context.AgentThought}
        **Action:** {context.AgentAction}
        **Observation:** {context.Observation}
        
        {(context.FullResponse != null ? $"## Full Response\n{context.FullResponse}" : "")}
        
        Evaluate whether the agent's reasoning and actions comply with the business rule above.
        """;

    private static (double Score, string Analysis) ParseCritiqueResponse(string response)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..(jsonEnd + 1)];
                var doc = System.Text.Json.JsonDocument.Parse(json);

                var score = doc.RootElement.TryGetProperty("score", out var scoreProp)
                    ? scoreProp.GetDouble()
                    : 0.0;

                var analysis = doc.RootElement.TryGetProperty("analysis", out var analysisProp)
                    ? analysisProp.GetString() ?? "No analysis provided"
                    : "No analysis provided";

                // Clamp score to valid range
                score = Math.Clamp(score, 0.0, 1.0);

                return (score, analysis);
            }
        }
        catch
        {
            // Fall through to default
        }

        // If parsing fails, treat as a failed evaluation
        return (0.0, $"Failed to parse evaluator response: {response[..Math.Min(200, response.Length)]}");
    }
}
