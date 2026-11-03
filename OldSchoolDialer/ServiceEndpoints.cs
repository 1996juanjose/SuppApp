namespace OldSchoolDialer;

public static class ServiceEndpoints
{
    public static readonly Uri Auth = new("https://ventas-authservice.wv6a0u.easypanel.host/");
    public static readonly Uri Api = new("https://ventas-oldschool-api.wv6a0u.easypanel.host/");
    public static HttpClient CreateClient() => new() { Timeout = TimeSpan.FromSeconds(30) };
}
