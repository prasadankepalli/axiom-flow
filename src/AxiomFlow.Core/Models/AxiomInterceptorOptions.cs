namespace AxiomFlow.Core.Models;

/// <summary>
/// Configuration options for the Axiom interceptor behavior.
/// Supports the configurable block vs. flag requirement.
/// </summary>
public sealed class AxiomInterceptorOptions
{
    /// <summary>
    /// Configuration section name for binding from appsettings.
    /// </summary>
    public const string SectionName = "AxiomFlow";

    /// <summary>
    /// The behavior when an invariant fails validation.
    /// </summary>
    public FailureBehavior OnFailure { get; set; } = FailureBehavior.Flag;

    /// <summary>
    /// Global pass threshold override. If set, overrides individual evaluator thresholds.
    /// Range: 0.0 to 1.0.
    /// </summary>
    public double? GlobalPassThreshold { get; set; }

    /// <summary>
    /// Whether to persist validation artifacts to the Semantic Ledger.
    /// </summary>
    public bool EnableLedger { get; set; } = true;

    /// <summary>
    /// Whether to emit OpenTelemetry spans for each validation.
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;

    /// <summary>
    /// Maximum time (in seconds) to wait for all evaluators to complete.
    /// </summary>
    public int EvaluationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Custom message returned when a response is blocked.
    /// </summary>
    public string BlockedResponseMessage { get; set; } =
        "This response has been blocked by Axiom-Flow semantic validation. " +
        "The agent's output violated one or more business invariants.";
}

/// <summary>
/// Determines the interceptor's behavior when a validation fails.
/// </summary>
public enum FailureBehavior
{
    /// <summary>
    /// Block the response entirely and return a safe refusal.
    /// Similar to PromptInjectionFilter behavior.
    /// </summary>
    Block,

    /// <summary>
    /// Allow the response through but annotate it with validation warnings.
    /// The validation result is logged and persisted but does not block the user.
    /// </summary>
    Flag,

    /// <summary>
    /// Route the response to a human reviewer for manual approval.
    /// Requires an external approval workflow integration.
    /// </summary>
    Referral
}
