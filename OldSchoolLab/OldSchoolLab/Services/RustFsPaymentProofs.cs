using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace OldSchool.Storage;

// Both applications deploy independently; keep this file identical in web and API.
public sealed class RustFsPaymentProofs(IConfiguration config, ILogger<RustFsPaymentProofs> logger) : IDisposable
{
    public const string Route = "/rustfs-payment-proofs";
    public const string Prefix = "oldschool/payment-proofs/";
    public const int MaxBytes = 1024 * 1024;
    private AmazonS3Client? client;
    public bool Enabled => config.GetValue<bool>("ObjectStorage:Enabled");
    private string Bucket => config["ObjectStorage:Bucket"] ?? "forense-media";
    private string Required(string name) => !string.IsNullOrWhiteSpace(config[$"ObjectStorage:{name}"])
        ? config[$"ObjectStorage:{name}"]!
        : throw new InvalidOperationException($"Falta configurar ObjectStorage:{name} para RustFS.");
    private AmazonS3Client Client => client ??= new AmazonS3Client(
        new BasicAWSCredentials(Required("AccessKey"), Required("SecretKey")), new AmazonS3Config
        {
            ServiceURL = config["ObjectStorage:Endpoint"] ?? "https://forense9-rustfs-s3.wv6a0u.easypanel.host",
            AuthenticationRegion = config["ObjectStorage:Region"] ?? "us-east-1",
            ForcePathStyle = true
        });

    public static string NormalizeExtension(string? extension)
    {
        var value = (extension ?? string.Empty).Trim().ToLowerInvariant();
        if (value.StartsWith("image/", StringComparison.Ordinal))
            value = value[6..];

        return value.TrimStart('.') switch
        {
            "jpg" or "jpeg" => "jpg",
            "png" => "png",
            "webp" => "webp",
            _ => throw new InvalidOperationException("Solo se permiten imagenes JPG, PNG o WEBP.")
        };
    }

    public async Task<string> SaveAsync(byte[] bytes, string extension, CancellationToken cancellationToken = default)
    {
        if (!Enabled) throw new InvalidOperationException("RustFS no esta habilitado.");
        if (bytes.Length == 0 || bytes.Length > MaxBytes)
            throw new InvalidOperationException("El comprobante debe contener una imagen de hasta 1 MB.");
        var ext = NormalizeExtension(extension);
        var fileName = $"{Guid.NewGuid():N}.{ext}";
        using var stream = new MemoryStream(bytes, writable: false);
        try
        {
            await Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = Bucket, Key = Prefix + fileName, InputStream = stream,
                ContentType = ext == "jpg" ? "image/jpeg" : $"image/{ext}",
                DisablePayloadSigning = true, ChecksumAlgorithm = ChecksumAlgorithm.SHA256
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is AmazonS3Exception or HttpRequestException)
        {
            logger.LogError(ex, "No se pudo guardar el comprobante en RustFS");
            throw new InvalidOperationException("No se pudo guardar el comprobante en RustFS. Intenta nuevamente.", ex);
        }
        return $"{Route}/{fileName}";
    }

    public async Task<IResult> OpenAsync(string fileName, HttpContext context)
    {
        var parts = fileName.Split('.');
        if (!Enabled || parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out _)
            || parts[1] is not ("jpg" or "png" or "webp")) return Results.NotFound();
        context.Response.Headers.CacheControl = "no-store";
        var url = await Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = Bucket, Key = Prefix + fileName, Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(5)
        });
        return Results.Redirect(url);
    }
    public void Dispose() => client?.Dispose();
}
