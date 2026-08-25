using Microsoft.Win32;

namespace FFGuardian.PremiumWpf;

public interface IScanTargetSelector
{
    Task<string?> SelectAsync(CancellationToken cancellationToken);
}

public sealed class ScanTargetSelector : IScanTargetSelector
{
    public Task<string?> SelectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OpenFileDialog fileDialog = new()
        {
            Title = "Seleziona un file da analizzare",
            CheckFileExists = true,
            Multiselect = false,
            Filter = "Tutti i file (*.*)|*.*"
        };
        bool? fileSelected = fileDialog.ShowDialog();
        if (fileSelected == true) return Task.FromResult<string?>(fileDialog.FileName);

        OpenFolderDialog folderDialog = new()
        {
            Title = "Seleziona una cartella da analizzare",
            Multiselect = false
        };
        bool? folderSelected = folderDialog.ShowDialog();
        return Task.FromResult(folderSelected == true ? folderDialog.FolderName : null);
    }
}
