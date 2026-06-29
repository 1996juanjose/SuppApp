namespace OldSchoolLab.Services;

public sealed class ApiEndpointsOptions
{
    public const string SectionName = "ApiEndpoints";

    public string AuthServiceBaseUrl { get; set; } = "http://localhost:5086";

    public string GatewayBaseUrl { get; set; } = "http://localhost:5085";
}
