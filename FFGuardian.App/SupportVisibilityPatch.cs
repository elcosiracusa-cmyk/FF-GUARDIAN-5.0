namespace FFGuardian;

internal static class SupportVisibilityPatch
{
    internal static void Apply(Form form, Action openSupport)
    {
        Button support = new()
        {
            Name = "AlwaysVisibleSupportButton",
            Text = "✉  ASSISTENZA CLIENTI",
            Width = 230,
            Height = 46,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Left = Math.Max(10, form.ClientSize.Width - 250),
            Top = 94,
            BackColor = Color.FromArgb(62, 125, 0),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = true
        };
        support.FlatAppearance.BorderColor = Color.FromArgb(142, 255, 0);
        support.FlatAppearance.BorderSize = 2;
        support.FlatAppearance.MouseOverBackColor = Color.FromArgb(82, 155, 0);
        support.Click += (_, _) => openSupport();

        form.Controls.Add(support);
        support.BringToFront();

        form.Resize += (_, _) =>
        {
            support.Left = Math.Max(10, form.ClientSize.Width - support.Width - 20);
            support.Top = 94;
            support.BringToFront();
        };
    }
}
