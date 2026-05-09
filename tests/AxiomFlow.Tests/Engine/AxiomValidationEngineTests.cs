using AxiomFlow.Core.Contracts;
using AxiomFlow.Core.Engine;
using AxiomFlow.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AxiomFlow.Tests.Engine;

public class AxiomValidationEngineTests
{
    private readonly Mock<ILogger<AxiomValidationEngine>> _loggerMock = new();

    [Fact]
    public async Task ValidateAsync_WithNoInvariants_ReturnsEmptyResults()
    {
        // Arrange
        var engine = CreateEngine(
            evaluators: new[] { CreatePassingEvaluator("TestEval") },
            invariants: Array.Empty<ISemanticInvariant>());

        // Act
        var results = await engine.ValidateAsync("thought", "action", "observation");

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_WithPassingEvaluator_ReturnsPassingResult()
    {
        // Arrange
        var invariant = CreateInvariant("TestRule", shouldApply: true);
        var evaluator = CreatePassingEvaluator("TestEval");
        var engine = CreateEngine(new[] { evaluator }, new[] { invariant });

        // Act
        var results = await engine.ValidateAsync("thought", "action", "observation");

        // Assert
        results.Should().HaveCount(1);
        results[0].Passed.Should().BeTrue();
        results[0].Score.Should().BeGreaterThanOrEqualTo(0.7);
        results[0].EvaluatorName.Should().Be("TestEval");
    }

    [Fact]
    public async Task ValidateAsync_WithFailingEvaluator_ReturnsFailingResult()
    {
        // Arrange
        var invariant = CreateInvariant("TestRule", shouldApply: true);
        var evaluator = CreateFailingEvaluator("TestEval");
        var engine = CreateEngine(new[] { evaluator }, new[] { invariant });

        // Act
        var results = await engine.ValidateAsync("thought", "action", "observation");

        // Assert
        results.Should().HaveCount(1);
        results[0].Passed.Should().BeFalse();
        results[0].Score.Should().BeLessThan(0.7);
    }

    [Fact]
    public async Task ValidateAsync_InvariantNotApplicable_SkipsEvaluation()
    {
        // Arrange
        var invariant = CreateInvariant("TestRule", shouldApply: false);
        var evaluator = CreatePassingEvaluator("TestEval");
        var engine = CreateEngine(new[] { evaluator }, new[] { invariant });

        // Act
        var results = await engine.ValidateAsync("thought", "action", "observation");

        // Assert
        results.Should().BeEmpty("the invariant should not apply to this action");
    }

    [Fact]
    public async Task ValidateAsync_MultipleEvaluators_RunsAllAgainstEachInvariant()
    {
        // Arrange
        var invariant = CreateInvariant("TestRule", shouldApply: true);
        var eval1 = CreatePassingEvaluator("Eval1");
        var eval2 = CreatePassingEvaluator("Eval2");
        var engine = CreateEngine(new[] { eval1, eval2 }, new[] { invariant });

        // Act
        var results = await engine.ValidateAsync("thought", "action", "observation");

        // Assert
        results.Should().HaveCount(2);
        results.Select(r => r.EvaluatorName).Should().Contain(new[] { "Eval1", "Eval2" });
    }

    [Fact]
    public async Task ValidateAsync_EvaluatorThrows_ReturnsZeroScoreResult()
    {
        // Arrange
        var invariant = CreateInvariant("TestRule", shouldApply: true);
        var evaluatorMock = new Mock<IAxiomEvaluator>();
        evaluatorMock.Setup(e => e.Name).Returns("BrokenEval");
        evaluatorMock.Setup(e => e.EvaluateAsync(It.IsAny<InvariantContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Evaluator crashed"));

        var engine = CreateEngine(new[] { evaluatorMock.Object }, new[] { invariant });

        // Act
        var results = await engine.ValidateAsync("thought", "action", "observation");

        // Assert — fail-closed: exception produces a 0.0 score
        results.Should().HaveCount(1);
        results[0].Score.Should().Be(0.0);
        results[0].Passed.Should().BeFalse();
        results[0].Analysis.Should().Contain("exception");
    }

    [Fact]
    public async Task ValidateAsync_MultipleInvariants_FiltersApplicable()
    {
        // Arrange
        var applicable = CreateInvariant("ApplicableRule", shouldApply: true);
        var notApplicable = CreateInvariant("SkippedRule", shouldApply: false);
        var evaluator = CreatePassingEvaluator("TestEval");
        var engine = CreateEngine(new[] { evaluator }, new[] { applicable, notApplicable });

        // Act
        var results = await engine.ValidateAsync("thought", "discount_action", "observation");

        // Assert
        results.Should().HaveCount(1);
        results[0].InvariantName.Should().Be("ApplicableRule");
    }

    // ────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────

    private AxiomValidationEngine CreateEngine(
        IEnumerable<IAxiomEvaluator> evaluators,
        IEnumerable<ISemanticInvariant> invariants)
    {
        return new AxiomValidationEngine(evaluators, invariants, _loggerMock.Object);
    }

    private static IAxiomEvaluator CreatePassingEvaluator(string name)
    {
        var mock = new Mock<IAxiomEvaluator>();
        mock.Setup(e => e.Name).Returns(name);
        mock.Setup(e => e.EvaluateAsync(It.IsAny<InvariantContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvariantContext ctx, CancellationToken _) => new ValidationResult
            {
                Score = 0.95,
                Analysis = "All checks passed",
                EvaluatorName = name,
                InvariantName = ctx.InvariantName
            });
        return mock.Object;
    }

    private static IAxiomEvaluator CreateFailingEvaluator(string name)
    {
        var mock = new Mock<IAxiomEvaluator>();
        mock.Setup(e => e.Name).Returns(name);
        mock.Setup(e => e.EvaluateAsync(It.IsAny<InvariantContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvariantContext ctx, CancellationToken _) => new ValidationResult
            {
                Score = 0.2,
                Analysis = "Violation detected: business rule breached",
                EvaluatorName = name,
                InvariantName = ctx.InvariantName
            });
        return mock.Object;
    }

    private static ISemanticInvariant CreateInvariant(string name, bool shouldApply)
    {
        var mock = new Mock<ISemanticInvariant>();
        mock.Setup(i => i.Name).Returns(name);
        mock.Setup(i => i.Description).Returns($"Test invariant: {name}");
        mock.Setup(i => i.ShouldApply(It.IsAny<string>())).Returns(shouldApply);
        mock.Setup(i => i.BuildContext(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string thought, string action, string obs) => new InvariantContext
            {
                InvariantName = name,
                InvariantDescription = $"Test invariant: {name}",
                AgentThought = thought,
                AgentAction = action,
                Observation = obs
            });
        return mock.Object;
    }
}
