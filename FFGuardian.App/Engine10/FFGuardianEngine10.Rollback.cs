using Microsoft.Win32;

namespace FFGuardian.Engine10;

internal sealed partial class FFGuardianEngine10
{
    public async Task<RollbackRecord10> BackupFileForRollbackAsync(
        string path,
        string action,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await ExecuteExclusiveAsync(
            token => _rollbackManager.BackupFileAsync(path, action, token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<RollbackRecord10> BackupRegistryValueForRollbackAsync(
        RegistryHive hive,
        RegistryView view,
        string keyPath,
        string valueName,
        string action,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await ExecuteExclusiveAsync(
            token => _rollbackManager.BackupRegistryValueAsync(
                hive, view, keyPath, valueName, action, token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<RollbackRecord10> BackupServiceForRollbackAsync(
        string serviceName,
        string action,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await ExecuteExclusiveAsync(
            token => _rollbackManager.BackupServiceAsync(serviceName, action, token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<RollbackRecord10> BackupScheduledTaskForRollbackAsync(
        string taskName,
        string action,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await ExecuteExclusiveAsync(
            token => _rollbackManager.BackupScheduledTaskAsync(taskName, action, token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreFileRollbackAsync(
        string rollbackId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await ExecuteExclusiveAsync(async token =>
        {
            RollbackRecord10 record = await _rollbackManager.GetRecordAsync(rollbackId, token).ConfigureAwait(false);
            await _rollbackManager.RestoreFileAsync(record, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreRegistryRollbackAsync(
        string rollbackId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await ExecuteExclusiveAsync(async token =>
        {
            RollbackRecord10 record = await _rollbackManager.GetRecordAsync(rollbackId, token).ConfigureAwait(false);
            await _rollbackManager.RestoreRegistryValueAsync(record, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreServiceRollbackAsync(
        string rollbackId,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!confirmed)
            throw new InvalidOperationException("Il ripristino del servizio richiede conferma esplicita.");

        await ExecuteExclusiveAsync(async token =>
        {
            RollbackRecord10 record = await _rollbackManager.GetRecordAsync(rollbackId, token).ConfigureAwait(false);
            await _rollbackManager.RestoreServiceAsync(record, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreScheduledTaskRollbackAsync(
        string rollbackId,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!confirmed)
            throw new InvalidOperationException("Il ripristino dell'attività pianificata richiede conferma esplicita.");

        await ExecuteExclusiveAsync(async token =>
        {
            RollbackRecord10 record = await _rollbackManager.GetRecordAsync(rollbackId, token).ConfigureAwait(false);
            await _rollbackManager.RestoreScheduledTaskAsync(record, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
