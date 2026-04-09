namespace MemberSmsCampaign.Infrastructure.Services;

public record ValidationError(string Field, string Issue);

public static class ValidationService
{
    private static readonly HashSet<string> ValidCampaignTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "welcome", "referral", "utilization", "holiday"
    };

    public static List<ValidationError> ValidateCampaignInput(string? name, string? type, string? messageTemplate)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(name))
            errors.Add(new ValidationError("name", "Name is required."));

        if (string.IsNullOrWhiteSpace(type))
            errors.Add(new ValidationError("type", "Campaign type is required."));
        else if (!ValidCampaignTypes.Contains(type))
            errors.Add(new ValidationError("type", $"Invalid campaign type '{type}'. Allowed types: welcome, referral, utilization, holiday."));

        if (string.IsNullOrWhiteSpace(messageTemplate))
            errors.Add(new ValidationError("messageTemplate", "Message template is required."));
        else
            errors.AddRange(ValidateMessageLength(messageTemplate));

        return errors;
    }

    public static List<ValidationError> ValidateMessageLength(string message)
    {
        var errors = new List<ValidationError>();

        if (message.Length > 160)
            errors.Add(new ValidationError("messageTemplate", "Message template must not exceed 160 characters."));

        return errors;
    }
}
