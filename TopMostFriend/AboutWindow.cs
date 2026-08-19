using System;
using System.Drawing;
using System.Windows.Forms;

namespace TopMostFriend;

public sealed class AboutWindow : Form
{
    public static void Display()
    {
        using AboutWindow about = new();
        about.ShowDialog();
    }

    public AboutWindow()
    {
        Text = Locale.String("AboutTitle");
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(440, 200);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        Icon = AppIcon.Window;

        Label title = new()
        {
            Text = $"{Program.TITLE} v{Application.ProductVersion}",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(18, 18),
        };

        Label description = new()
        {
            Text = Locale.String("AboutDescription"),
            AutoSize = false,
            Location = new Point(18, 50),
            Size = new Size(404, 70),
        };

        Button website = new()
        {
            Text = Locale.String("AboutWebsite"),
            Size = new Size(90, 27),
            Location = new Point(18, 132),
        };
        website.Click += (_, _) => ShellHelper.OpenUrl("https://github.com/flashwave/topmostfriend");

        Button donate = new()
        {
            Text = Locale.String("AboutDonate"),
            Size = new Size(90, 27),
            Location = new Point(114, 132),
        };
        donate.Click += (_, _) => ShellHelper.OpenUrl("https://flash.moe/donate");

        Button close = new()
        {
            Text = Locale.String("AboutClose"),
            Size = new Size(90, 27),
            Location = new Point(ClientSize.Width - 108, 132),
            DialogResult = DialogResult.OK,
        };

        AcceptButton = close;
        CancelButton = close;
        Controls.AddRange(new Control[] { title, description, website, donate, close });
    }
}
