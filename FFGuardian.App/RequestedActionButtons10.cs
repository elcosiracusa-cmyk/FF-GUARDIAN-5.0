using FFGuardian.Engine10;

namespace FFGuardian;

internal static class RequestedActionButtons10
{
    private static readonly Color Surface = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);

    public static void Attach(IndependentMainForm100 form, FFGuardianEngine10 engine)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(engine);

        RenameButton(form, "GESTISCI QUARANTENA", "QUARANTENA");
        RenameButton(form, "RICARICA DATABASE FIRME", "AGGIORNA FIRME");

        TabControl? tabs = FindControl<TabControl>(form);
        TabPage? auditPage = tabs?.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => string.Equals(page.Text, "AUDIT", StringComparison.OrdinalIgnoreCase));
        FlowLayoutPanel? commandBar = auditPage is null ? null : FindControl<FlowLayoutPanel>(auditPage);
        if (commandBar is null || ContainsButton(commandBar, "CONTROLLO AVVIO"))
            return;

        Button startupButton = CreateButton("CONTROLLO AVVIO");
        startupButton.Click += async (_, _) => await ExecuteButtonAsync(
            startupButton, () => RunStartupCheckAsync(form, engine));
        commandBar.Controls.Add(startupButton);
        commandBar.Controls.SetChildIndex(startupButton, 1);
    }

    private static async Task RunStartupCheckAsync(IWin32Window owner, FFGuardianEngine10 engine)
    {
        using ProgressDialog10 progress = new("CONTROLLO AVVIO");
        progress.Show(owner);

        EngineAuditResult10 result;
        try
        {
            Progress<string> status = new(progress.SetStatus);
            result = await engine.RunAuditAsync(status, progress.Token);
        }
        finally
        {
            progress.Close();
        }

        AuditFinding10[] startupFindings = result.Findings
            .Where(finding =>
                finding.Category.Contains("Persist", StringComparison.OrdinalIgnoreCase) ||
                finding.Category.Contains("Startup", StringComparison.OrdinalIgnoreCase) ||
                finding.Category.Contains("Avvio", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(finding => finding.RiskScore)
            .ToArray();

        using Form dialog = new()
        {
            Text = "CONTROLLO AVVIO",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(1180, 680),
            MinimumSize = new Size(800, 500),
            BackColor = Color.FromArgb(3, 8, 12),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F)
        };

        DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.FromArgb(3, 8, 12),
            ForeColor = Color.White,
            GridColor = Color.FromArgb(58, 76, 84),
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        grid.Columns.Add("Risk", "RISCHIO");
        grid.Columns.Add("Name", "ELEMENTO");
        grid.Columns.Add("Target", "PERCORSO / COMANDO");
        grid.Columns.Add("Signature", "FIRMA");
        grid.Columns.Add("Evidence", "DETTAGLI");

        foreach (AuditFinding10 finding in startupFindings)
            grid.Rows.Add(finding.RiskScore, finding.Name, finding.Target, finding.SignatureStatus, finding.Evidence);

        dialog.Controls.Add(grid);
        dialog.ShowDialog(owner);

        MessageBox.Show(owner,
            $"Elementi di avvio controllati: {result.PersistenceItems}\nSegnalazioni mostrate: {startupFindings.Length}\nPunteggio sicurezza: {result.SecurityScore}/100",
            "FF GUARDIAN 10 — Controllo avvio completato",
            MessageBoxButtons.OK,
            startupFindings.Any(finding => finding.RiskScore >= 60) ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private static void RenameButton(Control root, string currentText, string requestedText)
    {
        foreach (Button button in FindControls<Button>(root))
        {
            if (string.Equals(button.Text, currentText, StringComparison.OrdinalIgnoreCase))
                button.Text = requestedText;
        }
    }

    private static bool ContainsButton(Control root, string text) =>
        FindControls<Button>(root).Any(button => string.Equals(button.Text, text, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        if (root is T match)
            yield return match;
        foreach (Control child in root.Controls)
        {
            foreach (T found in FindControls<T>(child))
                yield return found;
        }
    }

    private static T? FindControl<T>(Control root) where T : Control => FindControls<T>(root).FirstOrDefault();

    private static Button CreateButton(string text)
    {
        Button button = new()
        {
            Width = 210,
            Height = 42,
            Margin = new Padding(6),
            Text = text,
            BackColor = Surface,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        return button;
    }

    private static async Task ExecuteButtonAsync(Button button, Func<Task> action)
    {
        button.Enabled = false;
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            MessageBox.Show(ex.Message, "FF GUARDIAN 10 — Operazione non completata",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            button.Enabled = true;
        }
    }

    private sealed class ProgressDialog10 : Form
    {
        private readonly Label _label;
        private readonly CancellationTokenSource _cancellation = new();

        public ProgressDialog10(string title)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(660, 190);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(3, 8, 12);
            ForeColor = Color.White;

            _label = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                Text = "Preparazione…",
                TextAlign = ContentAlignment.MiddleLeft
            };
            Button cancel = CreateButton("ANNULLA");
            cancel.Dock = DockStyle.Bottom;
            cancel.Click += (_, _) => _cancellation.Cancel();
            Controls.Add(_label);
            Controls.Add(cancel);
        }

        public CancellationToken Token => _cancellation.Token;

        public void SetStatus(string status)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(() => SetStatus(status)));
                return;
            }
            _label.Text = status;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _cancellation.Dispose();
            base.Dispose(disposing);
        }
    }
}
