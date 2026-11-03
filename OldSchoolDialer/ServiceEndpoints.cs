using System.Net.Security;
using System.Security.Cryptography;

namespace OldSchoolDialer;

public static class ServiceEndpoints
{
    public static readonly Uri Auth = new("https://localhost:64491/");
    public static readonly Uri Api = new("https://localhost:7254/");

    public static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler();
#if DEBUG
        // Trust only this PC's public development certificate, only on these local endpoints.
        handler.ServerCertificateCustomValidationCallback = (request, certificate, _, errors) =>
            errors == SslPolicyErrors.None ||
            (errors == SslPolicyErrors.RemoteCertificateChainErrors && certificate is not null &&
             request.RequestUri is { Scheme: "https", Host: "localhost", Port: 64491 or 7254 } &&
             certificate.GetCertHashString(HashAlgorithmName.SHA256) ==
             "854598CAF91B9463FCEEC99231CC8999B65B6A243DCEE62A6B68ABE579195B05");
#endif
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }
}
