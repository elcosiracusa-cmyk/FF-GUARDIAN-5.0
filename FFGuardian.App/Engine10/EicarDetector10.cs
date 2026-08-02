using System.Text;

namespace FFGuardian.Engine10;

internal static class EicarDetector10
{
    // EICAR è un campione di test innocuo, non malware eseguibile.
    private static readonly byte[] Marker = Encoding.ASCII.GetBytes(
        "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

    public static async Task<bool> IsEicarAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FileInfo info = new(path);
        if (!info.Exists || info.Length == 0 || info.Length > 1024 * 1024)
            return false;

        byte[] buffer = new byte[(int)info.Length];
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: Math.Min(buffer.Length, 64 * 1024),
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            total += read;
        }

        return total >= Marker.Length && Contains(buffer.AsSpan(0, total), Marker);
    }

    private static bool Contains(ReadOnlySpan<byte> source, ReadOnlySpan<byte> marker)
    {
        if (marker.IsEmpty || source.Length < marker.Length)
            return false;

        for (int index = 0; index <= source.Length - marker.Length; index++)
        {
            if (source.Slice(index, marker.Length).SequenceEqual(marker))
                return true;
        }

        return false;
    }
}
