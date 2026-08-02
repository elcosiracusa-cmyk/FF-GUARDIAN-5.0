using System.Text.Json;
using FFGuardian.Engine10;

namespace FFGuardian;

internal sealed class SignatureUpdateSettings10
{
    public string ManifestUrl { get; set; } = string.Empty;
    public string PublicKeyPem { get; set; } = string.Empty;

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FF Guardian", "Engine10", "signature-update.json");

    public static SignatureUpdateSettings10 Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new SignatureUpdateSettings10();
            return JsonSerializer.Deserialize<SignatureUpdateSettings10>(File.ReadAllText(SettingsPath))
                ?? new SignatureUpdateSettings10();
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            return new SignatureUpdateSettings10();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal static class SignatureUpdateCenter10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);

    public static void Attach(IndependentMainForm100 form, FFGuardianEngine10 engine)
    {
        TabControl? tabs = FindControl<TabControl>(form);
        TabPage? updatesPage = tabs?.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => string.Equals(page.Text, "AGGIORNAMENTI", StringComparison.OrdinalIgnoreCase));
        if (updatesPage is null || FindControls<Button>(updatesPage)
            .Any(button => button.Text == "VERIFICA AGGIORNAMENTI FIRME"))
            return;

        SignatureUpdateSettings10 settings = SignatureUpdateSettings10.Load();
        FlowLayoutPanel? panel = FindControl<FlowLayoutPanel>(updatesPage);
        if (panel is null) return;

        Label status = new()
        {
            Width = 820,
            Height = 58,
            BackColor = Surface,
            ForeColor = engine.SignatureDatabaseIsStale ? Color.Orange : Neon,
            Padding = new Padding(14),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Text = BuildStatus(engine)
        };

        Button configure = CreateButton("CONFIGURA SERVER FIRME");
        configure.Click += (_, _) => ShowConfigurationDialog(form, settings);

        Button update = CreateButton("VERIFICA AGGIORNAMENTI FIRME", emphasized: true);
        update.Click += async (_, _) =>
        {
            update.Enabled = false;
            try
            {
                if (string.IsNullOrWhiteSpace(settings.ManifestUrl) || string.IsNullOrWhiteSpace(settings.PublicKeyPem))
                    throw new InvalidOperationException("Configura prima URL HTTPS del manifesto e chiave pubblica RSA.");

                if (!Uri.TryCreate(settings.ManifestUrl, UriKind.Absolute, out Uri? manifestUri))
                    throw new InvalidOperationException("URL del manifesto firme non valido.");

                SignatureUpdateResult10 result = await engine.UpdateSignatureDatabaseAsync(
                    manifestUri, settings.PublicKeyPem);
                status.Text = BuildStatus(engine) + Environment.NewLine + result.Status;
                status.ForeColor = engine.SignatureDatabaseIsStale ? Color.Orange : Neon;
                MessageBox.Show(form,
                    $"{result.Status}\nVersione installata: {result.InstalledVersion}",
                    "FF GUARDIAN — Aggiornamento firme",
                    MessageBoxButtons.OK,
                    result.Succeeded ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
                MessageBox.Show(form, ex.Message, "FF GUARDIAN — Aggiornamento firme non riuscito",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                update.Enabled = true;
            }
        };

        panel.Controls.Add(status);
        panel.Controls.Add(configure);
        panel.Controls.Add(update);
    }

    private static string BuildStatus(FFGuardianEngine10 engine) =>
        $"DATABASE FIRME: {engine.SignatureDatabaseVersion}  ·  " +
        $"Generato: {engine.SignatureDatabaseGeneratedUtc.ToLocalTime():dd/MM/yyyy HH:mm}  ·  " +
        (engine.SignatureDatabaseIsStale ? "STATO: OBSOLETO" : "STATO: AGGIORNATO");

    private static void ShowConfigurationDialog(IWin32Window owner, SignatureUpdateSettings10 settings)
    {
        using Form dialog = new()
        {
            Text = "CONFIGURA AGGIORNAMENTI FIRME",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(850, 620),
            MinimumSize = new Size(720, 520),
            BackColor = Background,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F)
        };

        TextBox url = new()
        {
            Dock = DockStyle.Top,
            Height = 34,
            Text = settings.ManifestUrl,
            BackColor = Surface,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        TextBox key = new()
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Text = settings.PublicKeyPem,
            BackColor = Surface,
            ForeColor = Color.White,
            Font = new Font("Consolas", 9F)
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(18),
            BackColor = Background
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.Controls.Add(new Label { Text = "URL HTTPS DEL MANIFESTO", Dock = DockStyle.Fill, ForeColor = Color.White }, 0, 0);
        layout.Controls.Add(url, 0, 1);
        layout.Controls.Add(new Label { Text = "CHIAVE PUBBLICA RSA PEM", Dock = DockStyle.Fill, ForeColor = Color.White }, 0, 2);
        layout.Controls.Add(key, 0, 3);

        FlowLayoutPanel commands = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Background
        };
        Button save = CreateButton("SALVA CONFIGURAZIONE");
        Button cancel = CreateButton("ANNULLA");
        save.Click += (_, _) =>
        {
            if (!Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out Uri? uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(dialog, "Inserisci un URL HTTPS valido.", "FF GUARDIAN",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!key.Text.Contains("BEGIN PUBLIC KEY", StringComparison.Ordinal))
            {
                MessageBox.Show(dialog, "Inserisci una chiave pubblica RSA in formato PEM.", "FF GUARDIAN",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            settings.ManifestUrl = url.Text.Trim();
            settings.PublicKeyPem = key.Text.Trim();
            settings.Save();
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        commands.Controls.Add(save);
        commands.Controls.Add(cancel);
        layout.Controls.Add(commands, 0, 4);
        dialog.Controls.Add(layout);
        dialog.ShowDialog(owner);
    }

    private static Button CreateButton(string text, bool emphasized = false)
    {
        Button button = new()
        {
            Width = emphasized ? 310 : 260,
            Height = 44,
            Margin = new Padding(6),
            Text = text,
            BackColor = emphasized ? Neon : Surface,
            ForeColor = emphasized ? Color.Black : Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        return button;
    }

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        if (root is T match) yield return match;
        foreach (Control child in root.Controls)
            foreach (T found in FindControls<T>(child))
                yield return found;
    }

    private static T? FindControl<T>(Control root) where T : Control => FindControls<T>(root).FirstOrDefault();
}
