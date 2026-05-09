using System.Diagnostics;
using AxiomFlow.Core.Contracts;
using AxiomFlow.Core.Engine;
using AxiomFlow.Core.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AxiomFlow.Core.Middleware;

/// <summary>
/// Agent pipeline middleware that intercepts agent responses and validates them
/// against registered semantic invariants before returning to the caller.
/// 
/// Mirrors the pattern established by PromptInjectionFilter in the AIUnderwriter project:
/// wraps AIAgent.RunAsync / RunStreamingAsync with pre/post validation logic.
/// 
/// Configurable behavior: Block, Flag, or Referral (via <see cref="AxiomInterceptorOptions"/>).
/// </summary>
public sealed class AxiomInterceptor
{
    private readonly AxiomValidationEngine _engine;
    private readonly ISemanticLedger? _ledger;
    private readonly ILogger<AxiomInterceptor> _logger;
    private readonly AxiomInterceptorOptions _options;

    public AxiomInterceptor(
        AxiomValidationEngine engine,
        IOptions<AxiomInterceptorOptions> options,
        ILogger<AxiomInterceptor> logger,
        ISemanticLedger? ledger = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _ledger = ledger;
    }

    /// <summary>
    /// Non-streaming middleware delegate. Intercepts after agent execution,
    /// validates the response, and applies the configured failure behavior.
    /// </summary>
    public async Task<AgentResponse> InvokeAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        using var activity = AxiomValidationEngine.ActivitySource.StartActivity("AxiomFlow.Intercept");
        var sw = Stopwatch.StartNew();

        // --- Execute the inner agent ---
        var response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

        // --- Extract the agent's response text ---
        var responseText = ExtractResponseText(response);
        var userPrompt = ExtractUserPrompt(messages);
        var sessionId = session?.ToString() ?? Guid.NewGuid().ToString();

        activity?.SetTag("axiomflow.session_id", sessionId);

        // --- Validate against semantic invariants ---
        var results = await _engine.ValidateAsync(
            agentThought: responseText,
            agentAction: "agent_response",
            observation: responseText,
            cancellationToken: cancellationToken);

        sw.Stop();

        // --- Persist to the Semantic Ledger ---
        if (_options.EnableLedger && _ledger != null && results.Count > 0)
        {
            await PersistArtifactAsync(sessionId, userPrompt, responseText, results, sw.ElapsedMilliseconds, cancellationToken);
        }

        // --- Apply failure behavior ---
        var overallPass = results.Count == 0 || results.All(r => r.Passed);

        if (!overallPass)
        {
            return HandleFailure(results, response, activity);
        }

        _logger.LogInformation("[AxiomFlow] Response passed all semantic validations");
        return response;
    }

    /// <summary>
    /// Streaming middleware delegate. For streaming, validation occurs post-accumulation
    /// since we need the full response to validate.
    /// </summary>
    public async IAsyncEnumerable<AgentResponseUpdate> InvokeStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var accumulatedText = new System.Text.StringBuilder();
        var updates = new List<AgentResponseUpdate>();

        // --- Accumulate the full streaming response ---
        await foreach (var update in innerAgent.RunStreamingAsync(messages, session, options, cancellationToken))
        {
            updates.Add(update);
            if (update.Text is not null)
            {
                accumulatedText.Append(update.Text);
            }
        }

        var responseText = accumulatedText.ToString();
        var userPrompt = ExtractUserPrompt(messages);
        var sessionId = session?.ToString() ?? Guid.NewGuid().ToString();

        // --- Validate the accumulated response ---
        var results = await _engine.ValidateAsync(
            agentThought: responseText,
            agentAction: "agent_response",
            observation: responseText,
            cancellationToken: cancellationToken);

        sw.Stop();

        // --- Persist ---
        if (_options.EnableLedger && _ledger != null && results.Count > 0)
        {
            await PersistArtifactAsync(sessionId, userPrompt, responseText, results, sw.ElapsedMilliseconds, cancellationToken);
        }

        // --- Check validation ---
        var overallPass = results.Count == 0 || results.All(r => r.Passed);

        if (!overallPass && _options.OnFailure == FailureBehavior.Block)
        {
            _logger.LogError("[AxiomFlow] Streaming response BLOCKED. Failures: {Failures}",
                string.Join("; ", results.Where(r => !r.Passed).Select(r => $"{r.InvariantName}: {r.Analysis}")));

            throw new AxiomValidationException(
                _options.BlockedResponseMessage,
                results.Where(r => !r.Passed).ToList());
        }

        // --- Yield the accumulated updates (Flag or Referral modes pass through) ---
        foreach (var update in updates)
        {
            yield return update;
        }
    }

    private AgentResponse HandleFailure(
        IReadOnlyList<ValidationResult> results,
        AgentResponse originalResponse,
        Activity? activity)
    {
        var failures = results.Where(r => !r.Passed).ToList();
        var failureDetails = string.Join("; ", failures.Select(f => $"{f.InvariantName}: {f.Analysis}"));

        activity?.SetTag("axiomflow.failure_mode", _options.OnFailure.ToString());
        activity?.SetTag("axiomflow.failures", failureDetails);

        switch (_options.OnFailure)
        {
            case FailureBehavior.Block:
                _logger.LogError("[AxiomFlow] Response BLOCKED. Failures: {Failures}", failureDetails);
                return new AgentResponse(new List<ChatMessage>
                {
                    new(ChatRole.Assistant, _options.BlockedResponseMessage)
                });

            case FailureBehavior.Flag:
                _logger.LogWarning("[AxiomFlow] Response FLAGGED (pass-through). Failures: {Failures}", failureDetails);
                // Pass through the original response — the warning is logged and persisted
                return originalResponse;

            case FailureBehavior.Referral:
                _logger.LogWarning("[AxiomFlow] Response sent to REFERRAL. Failures: {Failures}", failureDetails);
                return new AgentResponse(new List<ChatMessage>
                {
                    new(ChatRole.Assistant,
                        $"[AXIOM-FLOW REFERRAL] This response requires human review. " +
                        $"Validation failures: {failureDetails}")
                });

            default:
                throw new ArgumentOutOfRangeException(nameof(_options.OnFailure));
        }
    }

    private async Task PersistArtifactAsync(
        string sessionId,
        string userPrompt,
        string agentResponse,
        IReadOnlyList<ValidationResult> results,
        long durationMs,
        CancellationToken cancellationToken)
    {
        try
        {
            // Group results by invariant for cleaner ledger entries
            var invariantGroups = results.GroupBy(r => r.InvariantName);

            foreach (var group in invariantGroups)
            {
                var artifact = new ValidationArtifact
                {
                    SessionId = sessionId,
                    UserPrompt = userPrompt,
                    AgentResponse = agentResponse,
                    InvariantName = group.Key,
                    Results = group.ToList().AsReadOnly(),
                    ValidationDurationMs = durationMs
                };

                await _ledger!.RecordAsync(artifact, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Ledger failure should never block the agent pipeline
            _logger.LogError(ex, "[AxiomFlow] Failed to persist validation artifact to ledger");
        }
    }

    private static string ExtractResponseText(AgentResponse response)
    {
        return string.Join("\n", response.Messages
            .Where(m => m.Role == ChatRole.Assistant)
            .Select(m => m.Text ?? string.Empty));
    }

    private static string ExtractUserPrompt(IEnumerable<ChatMessage> messages)
    {
        return messages
            .LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "[no user prompt]";
    }
}
