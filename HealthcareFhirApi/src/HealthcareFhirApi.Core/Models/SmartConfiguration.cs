// Feature: healthcare-fhir-api
using System.Text.Json.Serialization;

namespace HealthcareFhirApi.Core.Models;

public class SmartConfiguration
{
    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; set; } = default!;

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; } = default!;

    [JsonPropertyName("scopes_supported")]
    public string[] ScopesSupported { get; set; } = Array.Empty<string>();

    [JsonPropertyName("code_challenge_methods_supported")]
    public string[] CodeChallengeMethodsSupported { get; set; } = Array.Empty<string>();

    [JsonPropertyName("grant_types_supported")]
    public string[] GrantTypesSupported { get; set; } = Array.Empty<string>();

    [JsonPropertyName("capabilities")]
    public string[] Capabilities { get; set; } = Array.Empty<string>();
}
