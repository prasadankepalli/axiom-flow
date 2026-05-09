using AxiomFlow.Samples.Insurance.Invariants;
using FluentAssertions;
using Xunit;

namespace AxiomFlow.Tests.Invariants;

public class PremiumThresholdInvariantTests
{
    private readonly PremiumThresholdInvariant _invariant = new();

    [Fact]
    public void Name_ReturnsPremiumThreshold()
    {
        _invariant.Name.Should().Be("PremiumThreshold");
    }

    [Fact]
    public void Description_ContainsMaxDiscount()
    {
        _invariant.Description.Should().Contain("15%");
    }

    [Theory]
    [InlineData("discount", true)]
    [InlineData("premium", true)]
    [InlineData("pricing", true)]
    [InlineData("agent_response", true)]
    [InlineData("Apply 10% discount to the premium", true)]
    [InlineData("search_documents", false)]
    [InlineData("send_email", false)]
    public void ShouldApply_FiltersCorrectly(string action, bool expected)
    {
        _invariant.ShouldApply(action).Should().Be(expected);
    }

    [Fact]
    public void BuildContext_PopulatesAllFields()
    {
        // Act
        var context = _invariant.BuildContext(
            "I should apply a 10% discount",
            "apply_discount",
            "Discount of 10% applied");

        // Assert
        context.InvariantName.Should().Be("PremiumThreshold");
        context.AgentThought.Should().Contain("10%");
        context.AgentAction.Should().Be("apply_discount");
        context.Observation.Should().Contain("10%");
        context.Properties.Should().ContainKey("maxDiscountPercent");
        context.Properties!["maxDiscountPercent"].Should().Be(15.0);
    }

    [Fact]
    public void BuildContext_ExtractsDiscountValues()
    {
        // Act
        var context = _invariant.BuildContext(
            "Applying a 20% discount reduction to the base premium",
            "apply_discount",
            "Premium reduced by 20% discount");

        // Assert
        context.Properties.Should().ContainKey("extractedDiscounts");
        var discounts = (List<double>)context.Properties!["extractedDiscounts"];
        discounts.Should().Contain(20.0);
    }

    [Fact]
    public void BuildContext_Safe10PercentDiscount_ExtractsCorrectly()
    {
        // Act — scenario that SHOULD pass (10% < 15% limit)
        var context = _invariant.BuildContext(
            "Based on the client's loss history, I recommend a 10% discount off the base premium",
            "calculate_premium",
            "Applied 10% discount. New premium: $9,000");

        // Assert
        context.Properties.Should().ContainKey("extractedDiscounts");
    }
}
