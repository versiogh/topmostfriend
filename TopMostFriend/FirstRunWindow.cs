using System.Drawing;
using System.Windows.Forms;

namespace TopMostFriend;

public sealed class FirstRunWindow : Form
{
    public static void Display()
    {
        using FirstRunWindow firstRun = new();
        firstRun.ShowDialog();
    }

    private FirstRunWindow()
    {
        Text = Locale.String("FirstRunWelcomeTitle");
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(500, 190);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        Icon = AppIcon.Window;

        Label title = new()
        {
            Text = Program.TITLE,
            Font = new Font(Font.FontFamily, 16f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(18, 18),
        };

        Label explanation = new()
        {
            Text = Locale.String("FirstRunWelcomeIntro"),
            Location = new Point(18, 58),
            Size = new Size(464, 70),
        };

        Button settings = new()
        {
            Text = Locale.String("TraySettings").Replace("&", string.Empty),
            Location = new Point(18, 145),
            Size = new Size(110, 28),
        };
        settings.Click += (_, _) => SettingsWindow.Display();

        Button done = new()
        {
            Text = Locale.String("FirstRunContinue"),
            Location = new Point(372, 145),
            Size = new Size(110, 28),
            DialogResult = DialogResult.OK,
        };

        AcceptButton = done;
        Controls.AddRange(new Control[] { title, explanation, settings, done });
    }
}
