using Microsoft.Extensions.Configuration;
namespace Zyven.Infrastructure;

public interface IPrivateFileStore
{
    Task<string> Save(Stream source, CancellationToken ct);
    Task<Stream> Open(string key, CancellationToken ct);
}

public sealed class LocalPrivateFileStore : IPrivateFileStore
{
    private readonly string root;
    public LocalPrivateFileStore(IConfiguration configuration)
    {
        root = Path.GetFullPath(configuration["Files:PrivatePath"] ?? Path.Combine(AppContext.BaseDirectory, "private-files"));
        Directory.CreateDirectory(root);
    }
    public async Task<string> Save(Stream source, CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N"); var path = Path.Combine(root, key);
        await using var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, ct); return key;
    }
    public Task<Stream> Open(string key, CancellationToken ct)
    {
        if (key.Length != 32 || key.Any(c => !Uri.IsHexDigit(c))) throw new FileNotFoundException();
        Stream stream = new FileStream(Path.Combine(root, key), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }
}
