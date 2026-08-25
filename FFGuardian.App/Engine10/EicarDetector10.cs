using System.Text;

namespace FFGuardian.Engine10;

internal static class EicarDetector10
{
    // EICAR è un campione di test innocuo, non malware eseguibile. Non incorporare
    // però la firma completa come stringa contigua nell'assembly: un antimalware
    // dell'host può intercettare il binario di FFGuardian stesso prima di Main().
    // Le due metà vengono ricomposte in memoria e il confronto resta byte-per-byte
    // con la firma EICAR standard completa.
    private static readonly byte[] MarkerPrefix = Encoding.ASCII.GetBytes(
        "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-");
    private static readonly byte[] MarkerSuffix = Encoding.ASCII.GetBytes(
        "ANTIVIRUS-TEST-FILE!$H+H*");
    private static readonly byte[] Marker = BuildMarker();

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

    private static byte[] BuildMarker()
    {
        byte[] marker = new byte[MarkerPrefix.Length + MarkerSuffix.Length];
        Buffer.BlockCopy(MarkerPrefix, 0, marker, 0, MarkerPrefix.Length);
        Buffer.BlockCopy(MarkerSuffix, 0, marker, MarkerPrefix.Length, MarkerSuffix.Length);
        return marker;
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
