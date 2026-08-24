using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace FFGuardian.Security.Core;

public sealed class HardenedQuarantineService : IQuarantineService, IDisposable
{
    private const string Magic = "FFGQ20";
    private const int EncKeySize = 32;
    private const int AuthKeySize = 32;
    private const int IvSize = 16;
    private const int TagSize = 32;
    private static readonly byte[] Entropy = SHA256.HashData(Encoding.UTF8.GetBytes("FFGuardian.Security.Core.Quarantine.v2"));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _root;
    private readonly string _keyPath;
    private readonly IFileHashService _hashes;
    private readonly ISecurityEventLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public HardenedQuarantineService(IOptions<SecurityCoreOptions> options, IFileHashService hashes, ISecurityEventLogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _hashes = hashes ?? throw new ArgumentNullException(nameof(hashes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _root = Path.GetFullPath(Path.Combine(options.Value.DataDirectory, "Quarantine"));
        Directory.CreateDirectory(_root);
        _keyPath = Path.Combine(_root, "quarantine.key.dpapi");
        EnsureKey();
    }

    public async Task<QuarantineResult> QuarantineAsync(string path, string engine, string detection, string risk, CancellationToken cancellationToken)
    {
        string source = Path.GetFullPath(path);
        if (!File.Exists(source)) return new(false, null, "File sorgente assente.");
        if (IsInsideRoot(source)) return new(false, null, "Il file appartiene già alla quarantena protetta.");

        string sourceHash = await _hashes.ComputeSha256Async(source, cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[] master = ReadKey();
            try
            {
                QuarantineEntry? duplicate = await FindByHashAsync(sourceHash, master, cancellationToken).ConfigureAwait(false);
                if (duplicate is not null) return new(false, duplicate, "File già presente in quarantena.");

                Guid id = Guid.NewGuid();
                string payload = PayloadPath(id);
                string payloadTemp = payload + ".tmp";
                string metadata = MetadataPath(id);
                FileInfo info = new(source);
                TryDelete(payloadTemp);
                try
                {
                    await EncryptAsync(source, payloadTemp, master, cancellationToken).ConfigureAwait(false);
                    string verified = await DecryptAndHashAsync(payloadTemp, master, cancellationToken).ConfigureAwait(false);
                    if (!sourceHash.Equals(verified, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Verifica del contenitore cifrato non riuscita.");

                    File.Move(payloadTemp, payload, overwrite: false);
                    QuarantineEntry entry = new(id, info.Name, source, payload, sourceHash, info.Length, engine, detection, DateTimeOffset.UtcNow, risk);
                    await WriteMetadataAsync(metadata, entry, master, cancellationToken).ConfigureAwait(false);

                    File.Delete(source);
                    await _logger.LogAsync("Quarantine", "StoredEncrypted", id.ToString("N"), cancellationToken).ConfigureAwait(false);
                    return new(true, entry, "File cifrato e isolato.");
                }
                catch
                {
                    TryDelete(payloadTemp);
                    TryDelete(payload);
                    TryDelete(metadata);
                    throw;
                }
            }
            finally { CryptographicOperations.ZeroMemory(master); }
        }
        finally { _gate.Release(); }
    }

    public async Task<QuarantineResult> RestoreAsync(Guid id, string destinationPath, bool overwrite, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[] master = ReadKey();
            try
            {
                QuarantineEntry? entry = await GetAsync(id, master, cancellationToken).ConfigureAwait(false);
                if (entry is null) return new(false, null, "Elemento non trovato.");
                string payload = PayloadPath(id);
                if (!File.Exists(payload)) return new(false, entry, "Payload quarantena assente.");

                string destination = Path.GetFullPath(destinationPath);
                if (IsInsideRoot(destination)) return new(false, entry, "Ripristino dentro la quarantena non consentito.");
                if (File.Exists(destination) && !overwrite) return new(false, entry, "Destinazione già esistente.");
                string? directory = Path.GetDirectoryName(destination);
                if (directory is null) return new(false, entry, "Destinazione non valida.");
                Directory.CreateDirectory(directory);

                string temp = destination + ".ffguardian-restore.tmp";
                TryDelete(temp);
                try
                {
                    if (await IsEncryptedContainerAsync(payload, cancellationToken).ConfigureAwait(false))
                    {
                        await DecryptAsync(payload, temp, master, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // Compatibility path for pre-hardening quarantine records.
                        await CopyDurableAsync(payload, temp, cancellationToken).ConfigureAwait(false);
                    }

                    string restoredHash = await _hashes.ComputeSha256Async(temp, cancellationToken).ConfigureAwait(false);
                    if (!entry.Sha256.Equals(restoredHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Hash del file ripristinato non valido.");
                    File.Move(temp, destination, overwrite);
                }
                catch
                {
                    TryDelete(temp);
                    throw;
                }

                await _logger.LogAsync("Quarantine", "RestoredVerified", id.ToString("N"), cancellationToken).ConfigureAwait(false);
                return new(true, entry with { StoredPath = payload }, "File ripristinato dopo verifica crittografica/SHA-256.");
            }
            finally { CryptographicOperations.ZeroMemory(master); }
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[] master = ReadKey();
            try
            {
                QuarantineEntry? entry = await GetAsync(id, master, cancellationToken).ConfigureAwait(false);
                if (entry is null) return false;
                TryDelete(PayloadPath(id));
                TryDelete(MetadataPath(id));
                await _logger.LogAsync("Quarantine", "Deleted", id.ToString("N"), cancellationToken).ConfigureAwait(false);
                return true;
            }
            finally { CryptographicOperations.ZeroMemory(master); }
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<QuarantineEntry>> ListAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[] master = ReadKey();
            try
            {
                List<QuarantineEntry> entries = [];
                foreach (string file in Directory.EnumerateFiles(_root, "*.json", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out Guid id)) continue;
                    try
                    {
                        QuarantineEntry? entry = await ReadMetadataCompatAsync(file, id, master, cancellationToken).ConfigureAwait(false);
                        if (entry is null || !File.Exists(PayloadPath(id))) continue;
                        entries.Add(entry with { StoredPath = PayloadPath(id) });
                    }
                    catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
                    {
                    }
                }
                return entries.OrderByDescending(item => item.CreatedAt).ToArray();
            }
            finally { CryptographicOperations.ZeroMemory(master); }
        }
        finally { _gate.Release(); }
    }

    private async Task<QuarantineEntry?> FindByHashAsync(string hash, byte[] master, CancellationToken cancellationToken)
    {
        foreach (string file in Directory.EnumerateFiles(_root, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out Guid id)) continue;
            try
            {
                QuarantineEntry? entry = await ReadMetadataCompatAsync(file, id, master, cancellationToken).ConfigureAwait(false);
                if (entry is not null && File.Exists(PayloadPath(id)) && entry.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase))
                    return entry with { StoredPath = PayloadPath(id) };
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
            {
            }
        }
        return null;
    }

    private async Task<QuarantineEntry?> GetAsync(Guid id, byte[] master, CancellationToken cancellationToken)
    {
        string metadata = MetadataPath(id);
        if (!File.Exists(metadata)) return null;
        return await ReadMetadataCompatAsync(metadata, id, master, cancellationToken).ConfigureAwait(false);
    }

    private async Task<QuarantineEntry?> ReadMetadataCompatAsync(string path, Guid expectedId, byte[] master, CancellationToken cancellationToken)
    {
        byte[] document = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                AuthenticatedMetadata? envelope = JsonSerializer.Deserialize<AuthenticatedMetadata>(document, JsonOptions);
                if (envelope?.Entry is not null && !string.IsNullOrWhiteSpace(envelope.Hmac))
                {
                    ValidateMetadataEnvelope(envelope, master);
                    if (envelope.Entry.Id != expectedId) throw new InvalidDataException("Identificativo quarantena incoerente.");
                    return envelope.Entry with { StoredPath = PayloadPath(expectedId) };
                }
            }
            catch (JsonException)
            {
            }

            QuarantineEntry? legacy = JsonSerializer.Deserialize<QuarantineEntry>(document, JsonOptions);
            if (legacy is null || legacy.Id != expectedId) throw new InvalidDataException("Metadati legacy quarantena non validi.");
            string payload = PayloadPath(expectedId);
            if (!File.Exists(payload)) return null;
            string hash = await _hashes.ComputeSha256Async(payload, cancellationToken).ConfigureAwait(false);
            if (!hash.Equals(legacy.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Integrità record quarantena legacy non valida.");
            return legacy with { StoredPath = payload };
        }
        finally { CryptographicOperations.ZeroMemory(document); }
    }

    private string PayloadPath(Guid id) => SafePath(id.ToString("N") + ".qdat");
    private string MetadataPath(Guid id) => SafePath(id.ToString("N") + ".json");

    private string SafePath(string name)
    {
        string path = Path.GetFullPath(Path.Combine(_root, name));
        if (!IsInsideRoot(path)) throw new InvalidDataException("Percorso quarantena fuori dalla root protetta.");
        return path;
    }

    private bool IsInsideRoot(string candidate)
    {
        string full = Path.GetFullPath(candidate);
        return full.Equals(_root, StringComparison.OrdinalIgnoreCase) || full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureKey()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("La quarantena protetta richiede Windows DPAPI.");
        if (File.Exists(_keyPath))
        {
            byte[] existing = ReadKey();
            CryptographicOperations.ZeroMemory(existing);
            return;
        }

        byte[] key = RandomNumberGenerator.GetBytes(EncKeySize + AuthKeySize);
        byte[] protectedKey = ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser);
        string temp = _keyPath + ".tmp";
        try
        {
            WriteDurable(temp, protectedKey);
            try { File.Move(temp, _keyPath, overwrite: false); }
            catch (IOException) when (File.Exists(_keyPath)) { }
            try { File.SetAttributes(_keyPath, FileAttributes.Hidden); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(protectedKey);
            TryDelete(temp);
        }
    }

    private byte[] ReadKey()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("La quarantena protetta richiede Windows DPAPI.");
        byte[] protectedKey = File.ReadAllBytes(_keyPath);
        try
        {
            byte[] key = ProtectedData.Unprotect(protectedKey, Entropy, DataProtectionScope.CurrentUser);
            if (key.Length != EncKeySize + AuthKeySize)
            {
                CryptographicOperations.ZeroMemory(key);
                throw new InvalidDataException("Chiave master quarantena non valida.");
            }
            return key;
        }
        finally { CryptographicOperations.ZeroMemory(protectedKey); }
    }

    private static async Task EncryptAsync(string source, string destination, byte[] master, CancellationToken cancellationToken)
    {
        byte[] encKey = master[..EncKeySize];
        byte[] authKey = master[EncKeySize..];
        byte[] iv = RandomNumberGenerator.GetBytes(IvSize);
        byte[] magic = Encoding.ASCII.GetBytes(Magic);
        try
        {
            await using FileStream output = new(destination, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 131072,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);
            await output.WriteAsync(magic, cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(iv, cancellationToken).ConfigureAwait(false);
            using Aes aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = encKey;
            aes.IV = iv;
            await using (FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (CryptoStream crypto = new(output, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true))
            {
                await input.CopyToAsync(crypto, 131072, cancellationToken).ConfigureAwait(false);
                await crypto.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);
            }
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Flush(flushToDisk: true);
            output.Position = 0;
            using HMACSHA256 hmac = new(authKey);
            byte[] tag = await hmac.ComputeHashAsync(output, cancellationToken).ConfigureAwait(false);
            try
            {
                output.Position = output.Length;
                await output.WriteAsync(tag, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }
            finally { CryptographicOperations.ZeroMemory(tag); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encKey);
            CryptographicOperations.ZeroMemory(authKey);
            CryptographicOperations.ZeroMemory(iv);
        }
    }

    private static async Task DecryptAsync(string source, string destination, byte[] master, CancellationToken cancellationToken)
    {
        byte[] encKey = master[..EncKeySize];
        byte[] authKey = master[EncKeySize..];
        byte[] magic = new byte[Magic.Length];
        byte[] iv = new byte[IvSize];
        try
        {
            await VerifyContainerAsync(source, authKey, cancellationToken).ConfigureAwait(false);
            await using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await ReadExactlyAsync(input, magic, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(magic, Encoding.ASCII.GetBytes(Magic))) throw new InvalidDataException("Formato quarantena non riconosciuto.");
            await ReadExactlyAsync(input, iv, cancellationToken).ConfigureAwait(false);
            long cipherLength = input.Length - Magic.Length - IvSize - TagSize;
            if (cipherLength <= 0) throw new InvalidDataException("Contenitore quarantena troncato.");
            using Aes aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = encKey;
            aes.IV = iv;
            await using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);
            await using CryptoStream crypto = new(new LimitedReadStream(input, cipherLength), aes.CreateDecryptor(), CryptoStreamMode.Read);
            await crypto.CopyToAsync(output, 131072, cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Flush(flushToDisk: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encKey);
            CryptographicOperations.ZeroMemory(authKey);
            CryptographicOperations.ZeroMemory(magic);
            CryptographicOperations.ZeroMemory(iv);
        }
    }

    private async Task<string> DecryptAndHashAsync(string source, byte[] master, CancellationToken cancellationToken)
    {
        string temp = source + ".verify";
        TryDelete(temp);
        try
        {
            await DecryptAsync(source, temp, master, cancellationToken).ConfigureAwait(false);
            return await _hashes.ComputeSha256Async(temp, cancellationToken).ConfigureAwait(false);
        }
        finally { TryDelete(temp); }
    }

    private static async Task VerifyContainerAsync(string path, byte[] authKey, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length <= Magic.Length + IvSize + TagSize) throw new InvalidDataException("Contenitore quarantena non valido.");
        long authenticatedLength = stream.Length - TagSize;
        stream.Position = authenticatedLength;
        byte[] expected = new byte[TagSize];
        await ReadExactlyAsync(stream, expected, cancellationToken).ConfigureAwait(false);
        stream.Position = 0;
        using HMACSHA256 hmac = new(authKey);
        using LimitedReadStream limited = new(stream, authenticatedLength, leaveOpen: true);
        byte[] actual = await hmac.ComputeHashAsync(limited, cancellationToken).ConfigureAwait(false);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(actual, expected)) throw new InvalidDataException("Autenticazione del contenitore quarantena non valida.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
            CryptographicOperations.ZeroMemory(expected);
        }
    }

    private static async Task<bool> IsEncryptedContainerAsync(string path, CancellationToken cancellationToken)
    {
        byte[] expected = Encoding.ASCII.GetBytes(Magic);
        byte[] actual = new byte[expected.Length];
        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length < expected.Length) return false;
            await ReadExactlyAsync(stream, actual, cancellationToken).ConfigureAwait(false);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        finally { CryptographicOperations.ZeroMemory(actual); }
    }

    private static async Task WriteMetadataAsync(string path, QuarantineEntry entry, byte[] master, CancellationToken cancellationToken)
    {
        byte[] authKey = master[EncKeySize..];
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(entry, JsonOptions);
        try
        {
            using HMACSHA256 hmac = new(authKey);
            byte[] tag = hmac.ComputeHash(payload);
            try
            {
                byte[] document = JsonSerializer.SerializeToUtf8Bytes(new AuthenticatedMetadata(entry, Convert.ToBase64String(tag)), JsonOptions);
                try { await WriteDurableAsync(path, document, cancellationToken).ConfigureAwait(false); }
                finally { CryptographicOperations.ZeroMemory(document); }
            }
            finally { CryptographicOperations.ZeroMemory(tag); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(authKey);
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    private static void ValidateMetadataEnvelope(AuthenticatedMetadata envelope, byte[] master)
    {
        byte[] authKey = master[EncKeySize..];
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(envelope.Entry, JsonOptions);
        byte[] expected = Convert.FromBase64String(envelope.Hmac);
        try
        {
            using HMACSHA256 hmac = new(authKey);
            byte[] actual = hmac.ComputeHash(payload);
            try
            {
                if (expected.Length != TagSize || !CryptographicOperations.FixedTimeEquals(actual, expected))
                    throw new InvalidDataException("Integrità dei metadati quarantena non valida.");
            }
            finally { CryptographicOperations.ZeroMemory(actual); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(authKey);
            CryptographicOperations.ZeroMemory(payload);
            CryptographicOperations.ZeroMemory(expected);
        }
    }

    private static async Task WriteDurableAsync(string path, byte[] data, CancellationToken cancellationToken)
    {
        string temp = path + ".tmp";
        TryDelete(temp);
        try
        {
            await using (FileStream stream = new(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally { TryDelete(temp); }
    }

    private static async Task CopyDurableAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await input.CopyToAsync(output, 131072, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        output.Flush(flushToDisk: true);
    }

    private static void WriteDurable(string path, byte[] data)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        stream.Write(data);
        stream.Flush(flushToDisk: true);
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("Dati quarantena incompleti.");
            total += read;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record AuthenticatedMetadata(QuarantineEntry Entry, string Hmac);

    private sealed class LimitedReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _length;
        private readonly bool _leaveOpen;
        private long _remaining;
        private bool _disposed;

        public LimitedReadStream(Stream inner, long length, bool leaveOpen = false)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            _length = length;
            _remaining = length;
            _leaveOpen = leaveOpen;
        }

        public override bool CanRead => !_disposed && _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _length - _remaining; set => throw new NotSupportedException(); }
        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_remaining <= 0) return 0;
            int read = _inner.Read(buffer, offset, (int)Math.Min(count, _remaining));
            _remaining -= read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_remaining <= 0) return 0;
            int read = await _inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, _remaining)], cancellationToken).ConfigureAwait(false);
            _remaining -= read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                base.Dispose(disposing);
                return;
            }
            if (disposing && !_leaveOpen) _inner.Dispose();
            _disposed = true;
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                if (!_leaveOpen) await _inner.DisposeAsync().ConfigureAwait(false);
                _disposed = true;
            }
            await base.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}
