using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using TalentPilot.Application.Operations;

namespace TalentPilot.Infrastructure.Documents;

public sealed class LocalApplicationDocumentStorageOptions
{
    public string RootPath { get; init; } = string.Empty;
    public string StorageContainer { get; init; } = "application-documents";
}

public sealed class LocalApplicationDocumentStorage : IApplicationDocumentStorage
{
    public const string ProviderName = "LocalFileSystem";

    private readonly LocalApplicationDocumentStorageOptions _options;

    public LocalApplicationDocumentStorage(IOptions<LocalApplicationDocumentStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<StoredApplicationDocument> SaveAsync(
        StoreApplicationDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var rootPath = string.IsNullOrWhiteSpace(_options.RootPath)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "application-documents")
            : _options.RootPath;
        var extension = NormalizeExtension(Path.GetExtension(request.OriginalFileName));
        var documentId = Guid.NewGuid();
        var relativeDirectory = Path.Combine(
            request.TenantId.ToString("N"),
            request.JobApplicationId.ToString("N"));
        var absoluteDirectory = Path.Combine(rootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var fileName = $"{documentId:N}{extension}";
        var absolutePath = Path.Combine(absoluteDirectory, fileName);
        await File.WriteAllBytesAsync(absolutePath, request.Content, cancellationToken);

        var hash = Convert.ToHexString(SHA256.HashData(request.Content)).ToLowerInvariant();
        var storageKey = Path.Combine(relativeDirectory, fileName).Replace('\\', '/');

        return new StoredApplicationDocument(
            ProviderName,
            storageKey,
            _options.StorageContainer,
            request.Content.LongLength,
            hash);
    }

    public async Task<byte[]?> ReadAsync(
        string storageProvider,
        string storageKey,
        string? storageContainer,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(storageProvider, ProviderName, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(storageKey))
        {
            return null;
        }

        var rootPath = string.IsNullOrWhiteSpace(_options.RootPath)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "application-documents")
            : _options.RootPath;
        var normalizedKey = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var absoluteRoot = Path.GetFullPath(rootPath);
        var absolutePath = Path.GetFullPath(Path.Combine(absoluteRoot, normalizedKey));
        if (!absolutePath.StartsWith(absoluteRoot, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(absolutePath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(absolutePath, cancellationToken);
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".bin";
        }

        var safe = new string(extension
            .Where(character => char.IsLetterOrDigit(character) || character == '.')
            .ToArray());

        return safe.Length is > 1 and <= 12 ? safe.ToLowerInvariant() : ".bin";
    }
}
