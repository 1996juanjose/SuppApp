using Microsoft.AspNetCore.Http;

namespace OldSchoolLab.Services;

public interface IPaymentProofStorage
{
    long MaxFileSizeBytes { get; }
    Task<StoredPaymentProof?> SaveAsync(IFormFile? file, CancellationToken cancellationToken = default);
}

public sealed record StoredPaymentProof(string PublicPath, string OriginalFileName);

public class PaymentProofStorage(IWebHostEnvironment environment, OldSchool.Storage.RustFsPaymentProofs rustFs) : IPaymentProofStorage
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public long MaxFileSizeBytes => 1024 * 1024;

    public async Task<StoredPaymentProof?> SaveAsync(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("El comprobante no debe superar 1 MB.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Solo se permiten imágenes JPG, PNG o WEBP.");
        }

        if (rustFs.Enabled)
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            var path = await rustFs.SaveAsync(buffer.ToArray(), extension, cancellationToken);
            return new StoredPaymentProof(path, Path.GetFileName(file.FileName));
        }

        var folderPath = Path.Combine(environment.ContentRootPath, "storage", "payment-proofs");
        Directory.CreateDirectory(folderPath);

        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folderPath, safeFileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream, cancellationToken);

        return new StoredPaymentProof($"/payment-proofs/{safeFileName}", Path.GetFileName(file.FileName));
    }
}
