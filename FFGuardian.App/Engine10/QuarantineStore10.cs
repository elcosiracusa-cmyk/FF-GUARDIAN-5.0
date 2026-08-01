using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FFGuardian.Engine10;

internal sealed class QuarantineStore10
{
    private const string ContainerMagic = "FFGQ10";
    private const int EncryptionKeySize = 32;
    private const int AuthenticationKeySize = 32;
    private const int IvSize = 16;
    private const int HmacSize = 32;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _root;
    private readonly string _keyPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public QuarantineStore10(string? root = null)
    {
        _root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FF Guardian",
            "Engine10",
            "Quarantine");
        Directory.CreateDirectory(_root);
        _keyPath = Path.Combine(_root, "quarantine.key");
        EnsureKeyExists();
    }

    public async Task<QuarantineRecord10> QuarantineAsync(
        FileScanResult10 result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Verdict is not ThreatVerdict10.Malicious and not ThreatVerdict10.Suspicious)
            throw new InvalidOperationException("La quarantena è consentita solo per file sospetti o malevoli.");

        string source = Path.GetFullPath(result.Path);
        if (!File.Exists(source))
            throw new FileNotFoundException("File da mettere in quarantena non trovato.", source);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string sourceHash = await ComputeSha256Async(source, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(result.Sha256) &&
                !string.Equals(sourceHash, result.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Il file è cambiato dopo la scansione. Quarantena annullata.");

            string id = Guid.NewGuid().ToString("N");
            string folder = Path.Combine(_root, id);
            Directory.CreateDirectory(folder);

            string storedPath = Path.Combine(folder, "payload.ffgq");
            string metadataPath = Path.Combine(folder, "metadata.json");
            string metadataSignaturePath = Path.Combine(folder, "metadata.hmac");
            string tempPayload = storedPath + ".tmp";

            byte[] masterKey = await File.ReadAllBytesAsync(_keyPath, cancellationToken).ConfigureAwait(false);
            ValidateMasterKey(masterKey);

            try
            {
                await EncryptFileAsync(source, tempPayload, masterKey, cancellationToken).ConfigureAwait(false);
                string verifiedHash = await DecryptAndHashAsync(tempPayload, masterKey, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(sourceHash, verifiedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Verifica del contenitore cifrato non riuscita.");

                File.Move(tempPayload, storedPath, overwrite: true);

                QuarantineRecord10 record = new(
                    id,
                    source,
                    storedPath,
                    sourceHash,
                    result.DetectionName,
                    DateTime.UtcNow,
                    false);

                string metadataJson = JsonSerializer.Serialize(record, JsonOptions);
                await WriteAuthenticatedMetadataAsync(
                    metadataPath,
                    metadataSignaturePath,
                    metadataJson,
                    masterKey,
                    cancellationToken).ConfigureAwait(false);

                File.Delete(source);
                return record;
            }
            catch
            {
                TryDelete(tempPayload);
                TryDelete(storedPath);
                TryDelete(metadataPath);
                TryDelete(metadataSignaturePath);
                TryDeleteEmptyDirectory(folder);
                throw;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterKey);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RestoreAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || id.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Identificativo quarantena non valido.", nameof(id));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string folder = Path.Combine(_root, id);
            string metadataPath = Path.Combine(folder, "metadata.json");
            string metadataSignaturePath = Path.Combine(folder, "metadata.hmac");
            if (!File.Exists(metadataPath) || !File.Exists(metadataSignaturePath))
                throw new FileNotFoundException("Metadati quarantena non trovati.", metadataPath);

            byte[] masterKey = await File.ReadAllBytesAsync(_keyPath, cancellationToken).ConfigureAwait(false);
            ValidateMasterKey(masterKey);

            try
            {
                string metadataJson = await ReadAuthenticatedMetadataAsync(
                    metadataPath,
                    metadataSignaturePath,
                    masterKey,
                    cancellationToken).ConfigureAwait(false);

                QuarantineRecord10 record = JsonSerializer.Deserialize<QuarantineRecord10>(metadataJson)
                    ?? throw new InvalidDataException("Metadati quarantena non validi.");

                if (!string.Equals(record.Id, id, StringComparison.Ordinal))
                    throw new InvalidDataException("Identificativo quarantena incoerente.");
                if (record.Restored)
                    return;
                if (!File.Exists(record.StoredPath))
                    throw new FileNotFoundException("Contenuto quarantena non trovato.", record.StoredPath);

                string? destinationFolder = Path.GetDirectoryName(record.OriginalPath);
                if (!string.IsNullOrWhiteSpace(destinationFolder))
                    Directory.CreateDirectory(destinationFolder);
                if (File.Exists(record.OriginalPath))
                    throw new IOException("Nel percorso originale esiste già un file. Ripristino annullato.");

                string restoreTemp = record.OriginalPath + ".ffguardian-restore.tmp";
                TryDelete(restoreTemp);
                try
                {
                    await DecryptFileAsync(record.StoredPath, restoreTemp, masterKey, cancellationToken).ConfigureAwait(false);
                    string restoredHash = await ComputeSha256Async(restoreTemp, cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(restoredHash, record.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Integrità del file ripristinato non valida.");

                    File.Move(restoreTemp, record.OriginalPath);
                    QuarantineRecord10 updated = record with { Restored = true };
                    string updatedJson = JsonSerializer.Serialize(updated, JsonOptions);
                    await WriteAuthenticatedMetadataAsync(
                        metadataPath,
                        metadataSignaturePath,
                        updatedJson,
                        masterKey,
                        cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    TryDelete(restoreTemp);
                    throw;
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterKey);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureKeyExists()
    {
        if (File.Exists(_keyPath))
        {
            byte[] existing = File.ReadAllBytes(_keyPath);
            try { ValidateMasterKey(existing); }
            finally { CryptographicOperations.ZeroMemory(existing); }
            return;
        }

        byte[] key = RandomNumberGenerator.GetBytes(EncryptionKeySize + AuthenticationKeySize);
        string temp = _keyPath + ".tmp";
        try
        {
            File.WriteAllBytes(temp, key);
            File.Move(temp, _keyPath, overwrite: false);
            try { File.SetAttributes(_keyPath, FileAttributes.Hidden); } catch { }
        }
        catch (IOException) when (File.Exists(_keyPath))
        {
            TryDelete(temp);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static async Task EncryptFileAsync(
        string sourcePath,
        string destinationPath,
        byte[] masterKey,
        CancellationToken cancellationToken)
    {
        byte[] encryptionKey = masterKey[..EncryptionKeySize];
        byte[] authenticationKey = masterKey[EncryptionKeySize..];
        byte[] iv = RandomNumberGenerator.GetBytes(IvSize);
        byte[] magic = Encoding.ASCII.GetBytes(ContainerMagic);

        try
        {
            await using FileStream output = new(
                destinationPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

            await output.WriteAsync(magic, cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(iv, cancellationToken).ConfigureAwait(false);

            using Aes aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = encryptionKey;
            aes.IV = iv;

            await using (FileStream input = new(
                sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (CryptoStream crypto = new(
                output, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true))
            {
                await input.CopyToAsync(crypto, 128 * 1024, cancellationToken).ConfigureAwait(false);
                await crypto.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);
            }

            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Position = 0;
            using HMACSHA256 hmac = new(authenticationKey);
            byte[] tag = await hmac.ComputeHashAsync(output, cancellationToken).ConfigureAwait(false);
            output.Position = output.Length;
            await output.WriteAsync(tag, cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(authenticationKey);
            CryptographicOperations.ZeroMemory(iv);
        }
    }

    private static async Task DecryptFileAsync(
        string containerPath,
        string destinationPath,
        byte[] masterKey,
        CancellationToken cancellationToken)
    {
        byte[] encryptionKey = masterKey[..EncryptionKeySize];
        byte[] authenticationKey = masterKey[EncryptionKeySize..];
        try
        {
            await VerifyContainerHmacAsync(containerPath, authenticationKey, cancellationToken).ConfigureAwait(false);

            await using FileStream input = new(
                containerPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

            byte[] magic = new byte[ContainerMagic.Length];
            await ReadExactlyAsync(input, magic, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(magic, Encoding.ASCII.GetBytes(ContainerMagic)))
                throw new InvalidDataException("Formato quarantena non riconosciuto.");

            byte[] iv = new byte[IvSize];
            await ReadExactlyAsync(input, iv, cancellationToken).ConfigureAwait(false);
            long cipherLength = input.Length - ContainerMagic.Length - IvSize - HmacSize;
            if (cipherLength <= 0)
                throw new InvalidDataException("Contenitore quarantena troncato.");

            using Aes aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = encryptionKey;
            aes.IV = iv;

            await using FileStream output = new(
                destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using CryptoStream crypto = new(
                new LimitedReadStream(input, cipherLength), aes.CreateDecryptor(), CryptoStreamMode.Read);
            await crypto.CopyToAsync(output, 128 * 1024, cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            CryptographicOperations.ZeroMemory(iv);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(authenticationKey);
        }
    }

    private static async Task<string> DecryptAndHashAsync(
        string containerPath,
        byte[] masterKey,
        CancellationToken cancellationToken)
    {
        string temp = containerPath + ".verify";
        TryDelete(temp);
        try
        {
            await DecryptFileAsync(containerPath, temp, masterKey, cancellationToken).ConfigureAwait(false);
            return await ComputeSha256Async(temp, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static async Task VerifyContainerHmacAsync(
        string path,
        byte[] authenticationKey,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length <= ContainerMagic.Length + IvSize + HmacSize)
            throw new InvalidDataException("Contenitore quarantena non valido.");

        long authenticatedLength = stream.Length - HmacSize;
        stream.Position = authenticatedLength;
        byte[] expected = new byte[HmacSize];
        await ReadExactlyAsync(stream, expected, cancellationToken).ConfigureAwait(false);

        stream.Position = 0;
        using HMACSHA256 hmac = new(authenticationKey);
        byte[] actual = await hmac.ComputeHashAsync(
            new LimitedReadStream(stream, authenticatedLength), cancellationToken).ConfigureAwait(false);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
                throw new InvalidDataException("Autenticazione del contenitore quarantena non valida.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
            CryptographicOperations.ZeroMemory(expected);
        }
    }

    private static async Task WriteAuthenticatedMetadataAsync(
        string metadataPath,
        string signaturePath,
        string json,
        byte[] masterKey,
        CancellationToken cancellationToken)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);
        byte[] authenticationKey = masterKey[EncryptionKeySize..];
        try
        {
            using HMACSHA256 hmac = new(authenticationKey);
            byte[] signature = hmac.ComputeHash(data);
            string metadataTemp = metadataPath + ".tmp";
            string signatureTemp = signaturePath + ".tmp";
            await File.WriteAllBytesAsync(metadataTemp, data, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(signatureTemp, signature, cancellationToken).ConfigureAwait(false);
            File.Move(metadataTemp, metadataPath, overwrite: true);
            File.Move(signatureTemp, signaturePath, overwrite: true);
            CryptographicOperations.ZeroMemory(signature);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(data);
            CryptographicOperations.ZeroMemory(authenticationKey);
        }
    }

    private static async Task<string> ReadAuthenticatedMetadataAsync(
        string metadataPath,
        string signaturePath,
        byte[] masterKey,
        CancellationToken cancellationToken)
    {
        byte[] data = await File.ReadAllBytesAsync(metadataPath, cancellationToken).ConfigureAwait(false);
        byte[] expected = await File.ReadAllBytesAsync(signaturePath, cancellationToken).ConfigureAwait(false);
        byte[] authenticationKey = masterKey[EncryptionKeySize..];
        try
        {
            using HMACSHA256 hmac = new(authenticationKey);
            byte[] actual = hmac.ComputeHash(data);
            try
            {
                if (expected.Length != HmacSize || !CryptographicOperations.FixedTimeEquals(actual, expected))
                    throw new InvalidDataException("Integrità dei metadati quarantena non valida.");
                return Encoding.UTF8.GetString(data);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actual);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(data);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(authenticationKey);
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("Dati di quarantena incompleti.");
            total += read;
        }
    }

    private static void ValidateMasterKey(byte[] key)
    {
        if (key.Length != EncryptionKeySize + AuthenticationKeySize)
            throw new InvalidDataException("Chiave quarantena non valida.");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try { if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path); } catch { }
    }

    private sealed class LimitedReadStream : Stream
    {
        private readonly Stream _inner;
        private long _remaining;

        public LimitedReadStream(Stream inner, long length)
        {
            _inner = inner;
            _remaining = length;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _remaining;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining <= 0) return 0;
            int read = _inner.Read(buffer, offset, (int)Math.Min(count, _remaining));
            _remaining -= read;
            return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_remaining <= 0) return 0;
            int read = await _inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, _remaining)], cancellationToken).ConfigureAwait(false);
            _remaining -= read;
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
