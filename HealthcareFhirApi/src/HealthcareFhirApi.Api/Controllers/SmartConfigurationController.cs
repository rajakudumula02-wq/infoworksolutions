// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[AllowAnonymous]
public class SmartConfigurationController : ControllerBase
{
    private readonly IConfiguration _config;

    public SmartConfigurationController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>GET /.well-known/smart-configuration</summary>
    [HttpGet("/.well-known/smart-configuration")]
    [Produces("application/json")]
    public IActionResult GetSmartConfiguration()
    {
        var smartConfig = new SmartConfiguration
        {
            AuthorizationEndpoint       = _config["SmartAuth:AuthorizationEndpoint"]!,
            TokenEndpoint               = _config["SmartAuth:TokenEndpoint"]!,
            ScopesSupported             = new[] { "openid", "fhirUser", "launch", "launch/patient",
                                                   "patient/*.read", "user/*.read", "system/*.read",
                                                   "offline_access" },
            CodeChallengeMethodsSupported = new[] { "S256" },
            GrantTypesSupported         = new[] { "authorization_code", "client_credentials" },
            Capabilities                = new[] { "launch-ehr", "launch-standalone", "client-public",
                                                   "client-confidential-symmetric", "sso-openid-connect",
                                                   "permission-v2", "context-ehr-patient" }
        };

        return Ok(smartConfig);
    }
}
