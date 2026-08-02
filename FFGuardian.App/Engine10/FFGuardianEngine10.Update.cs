namespace FFGuardian.Engine10;

internal sealed partial class FFGuardianEngine10
{
    public bool SignatureDatabaseIsStale => _signatureDatabase.IsStale;
    public DateTime SignatureDatabaseGeneratedUtc => _signatureDatabase.GeneratedUtc;

    public async Task<SignatureUpdateResult10> UpdateSignatureDatabaseAsync(
        Uri manifestUri,
        string publicKeyPem,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(manifestUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);

        return await ExecuteExclusiveAsync(async token =>
        {
            using SignatureUpdateManager10 manager = new(publicKeyPem);
            return await manager.DownloadAndInstallAsync(manifestUri, _signatureDatabase, token)
                .ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UpdateStageResult10> DownloadAndStageUpdateAsync(
        UpdateManifest10 manifest,
        UpdateDownloadRequest10 request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(request);

        if (_secureUpdater is null)
        {
            return new UpdateStageResult10(
                false,
                "Chiave pubblica aggiornamenti non configurata.",
                string.Empty,
                string.Empty,
                request.InstalledVersion,
                request.InstalledVersion,
                string.Empty,
                DateTime.UtcNow);
        }

        return await ExecuteExclusiveAsync(
            token => _secureUpdater.DownloadAndStageAsync(manifest, request, token),
            cancellationToken).ConfigureAwait(false);
    }
}
