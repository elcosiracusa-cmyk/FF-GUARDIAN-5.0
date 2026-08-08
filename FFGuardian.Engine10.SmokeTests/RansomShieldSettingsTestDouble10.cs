namespace FFGuardian;

// Test-local runtime contract used to compile the Engine10 smoke suite without
// importing the WinForms Ransom Shield settings page from FFGuardian.App.
internal sealed class RansomShieldSettings10
{
    public bool Enabled { get; set; } = true;
    public bool ProtectPersonalFolders { get; set; } = true;
    public bool ShowAlerts { get; set; } = false;
    public int ChangeThreshold { get; set; } = 35;
    public int WindowSeconds { get; set; } = 15;
    public List<string> CustomFolders { get; set; } = [];

    public IEnumerable<string> GetProtectedFolders()
    {
        List<string> folders = [];
        if (ProtectPersonalFolders)
        {
            folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        }

        folders.AddRange(CustomFolders);
        return folders
            .Where(Directory.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
