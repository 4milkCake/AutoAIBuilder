using System.Security.Cryptography;

namespace AutoAIBuilder.Infrastructure.Automation;

internal static class FileChecksum
{
    public static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 131_072,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256
            .HashDataAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
