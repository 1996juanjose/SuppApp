using Microsoft.AspNetCore.Http;

namespace OldSchoolLab.Services;

public interface ICompanyLogoStorage
{
    long MaxFileSizeBytes { get; }
    Task<StoredCompanyLogo?> SaveAsync(IFormFile? file, CancellationToken cancellationToken = default);
}

public sealed record StoredCompanyLogo(string PublicPath, string OriginalFileName);

public class CompanyLogoStorage(IWebHostEnvironment environment) : ICompanyLogoStorage
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".svg"];

    public long MaxFileSizeBytes => 2 * 1024 * 1024;

    public async Task<StoredCompanyLogo?> SaveAsync(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("El logo no debe superar 2 MB.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Solo se permiten logos JPG, PNG, WEBP o SVG.");
        }

        var folderPath = Path.Combine(environment.ContentRootPath, "storage", "company-logos");
        Directory.CreateDirectory(folderPath);

        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folderPath, safeFileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream, cancellationToken);

        return new StoredCompanyLogo($"/company-logos/{safeFileName}", Path.GetFileName(file.FileName));
    }
}
