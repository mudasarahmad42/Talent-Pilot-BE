namespace TalentPilot.Application.Operations;

public sealed record StoreApplicationDocumentRequest(
    Guid TenantId,
    Guid JobApplicationId,
    string OriginalFileName,
    string ContentType,
    byte[] Content);

public sealed record StoredApplicationDocument(
    string StorageProvider,
    string StorageKey,
    string? StorageContainer,
    long SizeBytes,
    string ContentHashSha256);

public interface IApplicationDocumentStorage
{
    Task<StoredApplicationDocument> SaveAsync(
        StoreApplicationDocumentRequest request,
        CancellationToken cancellationToken);

    Task<byte[]?> ReadAsync(
        string storageProvider,
        string storageKey,
        string? storageContainer,
        CancellationToken cancellationToken);
}
