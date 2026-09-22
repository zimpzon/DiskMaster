namespace MainForm
{
    /// <summary>
    /// A color palette for one of the two app themes. Includes the owner-drawn tree's state colors
    /// alongside the ordinary control colors, since several of those (tuned for a white background)
    /// have poor contrast against a dark one - same category of problem as the selected-node
    /// contrast fix, just for the whole background instead of just the selection highlight.
    /// </summary>
    internal sealed class AppTheme
    {
        public required Color FormBackColor { get; init; }
        public required Color FormForeColor { get; init; }
        public required Color ControlBackColor { get; init; }
        public required Color ControlForeColor { get; init; }
        public required Color DisabledForeColor { get; init; }
        public required Color TreeBackColor { get; init; }
        public required Color TreeForeColor { get; init; }

        public required Color PendingColor { get; init; }
        public required Color ScanningColor { get; init; }
        public required Color SizeColor { get; init; }
        public required Color CountsColor { get; init; }
        public required Color WarmColor { get; init; }
        public required Color HotColor { get; init; }

        public static readonly AppTheme Light = new()
        {
            FormBackColor = SystemColors.Control,
            FormForeColor = SystemColors.ControlText,
            ControlBackColor = SystemColors.Window,
            ControlForeColor = SystemColors.WindowText,
            DisabledForeColor = SystemColors.GrayText,
            TreeBackColor = SystemColors.Window,
            TreeForeColor = SystemColors.WindowText,

            PendingColor = Color.Gray,
            ScanningColor = Color.DarkOrange,
            SizeColor = Color.DarkGreen,
            CountsColor = Color.SteelBlue,
            WarmColor = Color.IndianRed,
            HotColor = Color.Firebrick,
        };

        // Same background shades VS/VS Code use for their dark theme - a well-known, safe default
        // rather than inventing new ones. Status colors are lightened/brightened versions of the
        // light theme's so each keeps roughly the same "meaning" (green=done, orange=active, etc.)
        // while staying readable on near-black.
        public static readonly AppTheme Dark = new()
        {
            FormBackColor = Color.FromArgb(32, 32, 32),
            FormForeColor = Color.Gainsboro,
            ControlBackColor = Color.FromArgb(37, 37, 38),
            ControlForeColor = Color.Gainsboro,
            // Deliberately not derived from ControlBackColor via ControlPaint.Dark/Light (the
            // built-in "disabled text" mechanism) - see MainForm's button Paint override for why
            // that's unusable on a dark background.
            DisabledForeColor = Color.FromArgb(140, 140, 140),
            TreeBackColor = Color.FromArgb(30, 30, 30),
            TreeForeColor = Color.Gainsboro,

            PendingColor = Color.DarkGray,
            ScanningColor = Color.Orange,
            SizeColor = Color.LightGreen,
            CountsColor = Color.LightSkyBlue,
            WarmColor = Color.LightSalmon,
            HotColor = Color.OrangeRed,
        };
    }
}
