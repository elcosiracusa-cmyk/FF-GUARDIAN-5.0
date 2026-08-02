using System.Runtime.CompilerServices;

namespace FFGuardian;

internal static class HeaderReadabilityFix10
{
    private static bool _applied;

    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.Idle += ApplyWhenReady;
    }

    private static void ApplyWhenReady(object? sender, EventArgs e)
    {
        if (_applied)
            return;

        IndependentMainForm100? form = Application.OpenForms
            .OfType<IndependentMainForm100>()
            .FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated)
            return;

        Panel? header = FindControls<Panel>(form)
            .FirstOrDefault(panel => string.Equals(panel.Name, "PremiumHeader10", StringComparison.Ordinal));
        if (header is null)
            return;

        Apply(header, form);
        _applied = true;
        Application.Idle -= ApplyWhenReady;
        StabilityCoordinator82.WriteInformationLog("Correzione leggibilità intestazione FFGUARDIAN applicata.");
    }

    private static void Apply(Panel header, Form form)
    {
        header.SuspendLayout();
        try
        {
            header.Height = 138;
            header.Padding = new Padding(18, 10, 18, 10);

            TripleDobermannEmblem10? emblem = header.Controls
                .OfType<TripleDobermannEmblem10>()
                .FirstOrDefault();
            if (emblem is not null)
            {
                emblem.Width = 126;
                emblem.Margin = new Padding(0, 0, 12, 0);
            }

            Panel? status = header.Controls
                .OfType<Panel>()
                .FirstOrDefault(panel => string.Equals(panel.Name, "PremiumStatus10", StringComparison.Ordinal));
            if (status is not null)
            {
                status.Width = form.ClientSize.Width < 1250 ? 210 : 255;
                status.Padding = new Padding(14, 12, 14, 12);
            }

            Panel? oldTitlePanel = header.Controls
                .OfType<Panel>()
                .FirstOrDefault(panel => panel != status && panel.Controls.OfType<Label>()
                    .Any(label => string.Equals(label.Text, "FFGUARDIAN", StringComparison.OrdinalIgnoreCase)));

            if (oldTitlePanel is null)
                return;

            oldTitlePanel.Controls.Clear();
            oldTitlePanel.Padding = new Padding(18, 7, 12, 7);

            TableLayoutPanel titleLayout = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = oldTitlePanel.BackColor,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            titleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
            titleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            titleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

            Label slogan = new()
            {
                Dock = DockStyle.Fill,
                Text = "ULTIMATE PROTECTION  •  THREE DOBERMANN DEFENSE",
                ForeColor = Color.FromArgb(108, 255, 36),
                BackColor = oldTitlePanel.BackColor,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0)
            };

            Label title = new()
            {
                Dock = DockStyle.Fill,
                Text = "FFGUARDIAN",
                ForeColor = Color.White,
                BackColor = oldTitlePanel.BackColor,
                Font = new Font("Bahnschrift", form.ClientSize.Width < 1250 ? 25F : 31F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0)
            };

            Label subtitle = new()
            {
                Dock = DockStyle.Fill,
                Text = "Protezione autonoma  •  Ransom Shield  •  Firewall  •  USB Shield  •  Engine10",
                ForeColor = Color.FromArgb(190, 204, 212),
                BackColor = oldTitlePanel.BackColor,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0)
            };

            titleLayout.Controls.Add(slogan, 0, 0);
            titleLayout.Controls.Add(title, 0, 1);
            titleLayout.Controls.Add(subtitle, 0, 2);
            oldTitlePanel.Controls.Add(titleLayout);

            form.Resize += (_, _) =>
            {
                if (form.IsDisposed)
                    return;
                title.Font = new Font("Bahnschrift", form.ClientSize.Width < 1250 ? 25F : 31F, FontStyle.Bold);
                if (status is not null)
                    status.Width = form.ClientSize.Width < 1250 ? 210 : 255;
            };
        }
        finally
        {
            header.ResumeLayout(performLayout: true);
        }
    }

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
}
