using AxiomFlow.Core.Extensions;
using AxiomFlow.Core.Models;
using AxiomFlow.Persistence;
using AxiomFlow.Persistence.Extensions;
using AxiomFlow.Samples.Insurance.Evaluators;
using AxiomFlow.Samples.Insurance.Invariants;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────────────────────────────
// 1. AxiomFlow Core — Validation Engine + Interceptor
// ──────────────────────────────────────────────────────────────────────
builder.Services.AddAxiomFlow(options =>
{
    // Bind from configuration, with code defaults as fallback
    var section = builder.Configuration.GetSection(AxiomInterceptorOptions.SectionName);
    section.Bind(options);

    // Default to Flag mode — configurable via appsettings
    options.OnFailure = Enum.TryParse<FailureBehavior>(
        builder.Configuration["AxiomFlow:OnFailure"], true, out var behavior)
        ? behavior
        : FailureBehavior.Flag;
});

// 2. Register Semantic Invariants (Code-based)
// ──────────────────────────────────────────────────────────────────────
builder.Services.AddSemanticInvariant<PremiumThresholdInvariant>();

// 3. Register Dynamic Invariants (Policy-as-Code / DSL)
// ──────────────────────────────────────────────────────────────────────
// Loads rules from JSON, allowing business analysts to update guardrails without code changes.
builder.Services.AddAxiomPolicies("policies.json");

// 4. Register Evaluators
// ──────────────────────────────────────────────────────────────────────
// DiscountComplianceEvaluator: Deterministic regex-based evaluator (no LLM needed)
// For LLM-based evaluation, register AICriticEvaluator with an IChatClient.
builder.Services.AddAxiomEvaluator<DiscountComplianceEvaluator>();

// 5. Cosmos DB Semantic Ledger
// ──────────────────────────────────────────────────────────────────────
var cosmosSection = builder.Configuration.GetSection(CosmosLedgerOptions.SectionName);
if (cosmosSection.Exists())
{
    builder.Services.AddAxiomFlowCosmosLedger(options =>
    {
        cosmosSection.Bind(options);
    });
}

// 6. Telemetry — Structured JSON + OpenTelemetry
// ──────────────────────────────────────────────────────────────────────
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});

var app = builder.Build();

// ──────────────────────────────────────────────────────────────────────
// Static Dashboard (served from wwwroot/index.html)
// ──────────────────────────────────────────────────────────────────────
app.UseDefaultFiles();
app.UseStaticFiles();

// ──────────────────────────────────────────────────────────────────────
// Minimal API Endpoints
// ──────────────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));

/// <summary>
/// Validates an agent's response against registered semantic invariants.
/// This is the primary endpoint for testing the Axiom-Flow pipeline.
/// </summary>
app.MapPost("/api/validate", async (
    ValidateRequest request,
    AxiomFlow.Core.Engine.AxiomValidationEngine engine) =>
{
    var results = await engine.ValidateAsync(
        agentThought: request.AgentThought,
        agentAction: request.AgentAction,
        observation: request.Observation);

    var overallPass = results.Count == 0 || results.All(r => r.Passed);

    return Results.Ok(new
    {
        overallPass,
        resultCount = results.Count,
        results = results.Select(r => new
        {
            r.EvaluatorName,
            r.InvariantName,
            r.Score,
            r.Passed,
            r.Analysis,
            r.Timestamp
        })
    });
});

/// <summary>
/// Retrieves validation artifacts for a given session from the Semantic Ledger.
/// </summary>
app.MapGet("/api/sessions/{sessionId}/artifacts", async (
    string sessionId,
    AxiomFlow.Core.Contracts.ISemanticLedger ledger) =>
{
    var artifacts = await ledger.GetBySessionAsync(sessionId);
    return Results.Ok(artifacts);
});

/// <summary>
/// Returns all registered semantic invariants.
/// </summary>
app.MapGet("/api/invariants", (IEnumerable<AxiomFlow.Core.Contracts.ISemanticInvariant> invariants) =>
{
    return Results.Ok(invariants.Select(i => new
    {
        i.Name,
        i.Description
    }));
});

app.Run();

// ──────────────────────────────────────────────────────────────────────
// Request/Response Models
// ──────────────────────────────────────────────────────────────────────

/// <summary>
/// Request model for the /api/validate endpoint.
/// </summary>
public record ValidateRequest(
    string AgentThought,
    string AgentAction,
    string Observation);
