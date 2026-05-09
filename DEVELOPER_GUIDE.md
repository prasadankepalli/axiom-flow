# Axiom-Flow Developer Guide

This guide explains how to extend and customize the Axiom-Flow framework. Axiom-Flow is built for extensibility, allowing you to define your own business rules (Invariants) and evaluation strategies (Evaluators).

---

## 🏗 Extensibility Architecture

Axiom-Flow decouples **what** is being checked from **how** it is checked:

1.  **`ISemanticInvariant`**: Defines the business rule, the context it needs, and when it should be applied.
2.  **`IAxiomEvaluator`**: Implements a specific strategy to evaluate an invariant (e.g., LLM critique, Regex, Grounding check).

---

## 1. Creating a Custom Invariant

To create a new business rule, implement the `ISemanticInvariant` interface.

### Example: `PIIExclusionInvariant`

```csharp
public class PIIExclusionInvariant : ISemanticInvariant
{
    public string Name => "PIIExclusion";
    public string Description => "Ensure no PII (emails, SSNs) is leaked in the agent's thought process or response.";

    public InvariantContext BuildContext(string thought, string action, string observation)
    {
        return new InvariantContext
        {
            InvariantName = Name,
            InvariantDescription = Description,
            AgentThought = thought,
            AgentAction = action,
            Observation = observation
        };
    }

    public bool ShouldApply(string agentAction)
    {
        // Apply to all actions to ensure total PII safety
        return true;
    }
}
```

---

## 2. Creating a Custom Evaluator

To implement a new way of checking compliance, implement the `IAxiomEvaluator` interface.

### Example: `RegexEvaluator`

```csharp
public class RegexEvaluator : IAxiomEvaluator
{
    public string Name => "RegexEvaluator";

    public async Task<ValidationResult> EvaluateAsync(InvariantContext context, CancellationToken ct)
    {
        // Simple example: check for email patterns
        var emailRegex = new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
        bool hasEmail = emailRegex.IsMatch(context.AgentThought) || emailRegex.IsMatch(context.Observation);

        return new ValidationResult
        {
            Score = hasEmail ? 0.0 : 1.0,
            Analysis = hasEmail ? "PII detected: Email address found." : "No PII detected.",
            EvaluatorName = Name,
            InvariantName = context.InvariantName
        };
    }
}
```

---

## 3. Registering Components

Use the provided DI extensions in `Program.cs` to wire up your custom logic.

```csharp
// Register the framework
builder.Services.AddAxiomFlow(options => {
    options.OnFailure = FailureBehavior.Block;
});

// Register custom invariants
builder.Services.AddSemanticInvariant<PIIExclusionInvariant>();

// Register custom evaluators
builder.Services.AddAxiomEvaluator<RegexEvaluator>();
```

---

## 4. Using the Interceptor

The `AxiomInterceptor` is designed to work with the **Microsoft Agent Framework**. You can inject it into your agent pipeline or use it as a standalone service to validate agent outputs.

In a middleware-based agent setup:

```csharp
public AIAgent CreateSecureAgent(IChatClient client)
{
    var interceptor = serviceProvider.GetRequiredService<AxiomInterceptor>();
    
    return client.AsAIAgent(
        name: "SecureAgent",
        instructions: "...",
        // Register the interceptor in the agent pipeline
        middleware: (messages, session, options, agent, ct) => 
            interceptor.InvokeAsync(messages, session, options, agent, ct)
    );
}
```

---

## 5. Policy-as-Code (DSL)

Axiom-Flow allows you to define business rules in JSON without writing new C# classes. This is ideal for business analysts or runtime-configurable guardrails.

### Defining a Policy (`policies.json`)

```json
[
  {
    "name": "GlobalSafety",
    "description": "Ensure the agent never outputs offensive content.",
    "applyToActions": [ "*" ]
  },
  {
    "name": "FinancialAuthority",
    "description": "Limit total discount amount.",
    "applyToActions": [ "apply_discount", "calculate_premium" ]
  }
]
```

### Registering Policies

```csharp
// Loads all policies from the JSON file and registers them as DynamicSemanticInvariants
builder.Services.AddAxiomPolicies("policies.json");
```

---

## 6. Testing Your Extensions

Always add xUnit tests for new invariants and evaluators. Refer to the `tests/AxiomFlow.Tests` project for examples using **Moq** and **FluentAssertions**.

### Testing an Invariant
Ensure `ShouldApply` filters correctly and `BuildContext` captures all necessary data.

### Testing an Evaluator
Mock the dependencies (like `IChatClient` for AI evaluators) and verify the `ValidationResult.Score` for various scenarios.
