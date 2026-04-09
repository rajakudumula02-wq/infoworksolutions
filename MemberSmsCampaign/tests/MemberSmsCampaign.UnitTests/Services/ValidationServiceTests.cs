using MemberSmsCampaign.Infrastructure.Services;
using Xunit;

namespace MemberSmsCampaign.UnitTests.Services;

public class ValidationServiceTests
{
    [Fact]
    public void ValidateCampaignInput_ValidInput_ReturnsNoErrors()
    {
        var errors = ValidationService.ValidateCampaignInput("Test", "welcome", "Hello!");
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCampaignInput_MissingName_ReturnsNameError()
    {
        var errors = ValidationService.ValidateCampaignInput(null, "welcome", "Hello!");
        Assert.Single(errors);
        Assert.Equal("name", errors[0].Field);
    }

    [Fact]
    public void ValidateCampaignInput_MissingType_ReturnsTypeError()
    {
        var errors = ValidationService.ValidateCampaignInput("Test", null, "Hello!");
        Assert.Single(errors);
        Assert.Equal("type", errors[0].Field);
    }

    [Fact]
    public void ValidateCampaignInput_MissingMessageTemplate_ReturnsMessageError()
    {
        var errors = ValidationService.ValidateCampaignInput("Test", "welcome", null);
        Assert.Single(errors);
        Assert.Equal("messageTemplate", errors[0].Field);
    }

    [Fact]
    public void ValidateCampaignInput_AllFieldsMissing_ReturnsThreeErrors()
    {
        var errors = ValidationService.ValidateCampaignInput(null, null, null);
        Assert.Equal(3, errors.Count);
        Assert.Contains(errors, e => e.Field == "name");
        Assert.Contains(errors, e => e.Field == "type");
        Assert.Contains(errors, e => e.Field == "messageTemplate");
    }

    [Fact]
    public void ValidateCampaignInput_InvalidType_ReturnsTypeError()
    {
        var errors = ValidationService.ValidateCampaignInput("Test", "invalid", "Hello!");
        Assert.Single(errors);
        Assert.Equal("type", errors[0].Field);
        Assert.Contains("Invalid campaign type", errors[0].Issue);
    }

    [Theory]
    [InlineData("welcome")]
    [InlineData("referral")]
    [InlineData("utilization")]
    [InlineData("holiday")]
    public void ValidateCampaignInput_AllValidTypes_ReturnsNoErrors(string type)
    {
        var errors = ValidationService.ValidateCampaignInput("Test", type, "Hello!");
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("Welcome")]
    [InlineData("HOLIDAY")]
    [InlineData("Referral")]
    public void ValidateCampaignInput_CaseInsensitiveTypes_ReturnsNoErrors(string type)
    {
        var errors = ValidationService.ValidateCampaignInput("Test", type, "Hello!");
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateMessageLength_At160Chars_ReturnsNoErrors()
    {
        var message = new string('A', 160);
        var errors = ValidationService.ValidateMessageLength(message);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateMessageLength_At161Chars_ReturnsError()
    {
        var message = new string('A', 161);
        var errors = ValidationService.ValidateMessageLength(message);
        Assert.Single(errors);
        Assert.Equal("messageTemplate", errors[0].Field);
    }

    [Fact]
    public void ValidateMessageLength_ShortMessage_ReturnsNoErrors()
    {
        var errors = ValidationService.ValidateMessageLength("Hi");
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCampaignInput_MessageOver160_ReturnsMessageError()
    {
        var longMessage = new string('B', 161);
        var errors = ValidationService.ValidateCampaignInput("Test", "welcome", longMessage);
        Assert.Single(errors);
        Assert.Equal("messageTemplate", errors[0].Field);
    }
}
