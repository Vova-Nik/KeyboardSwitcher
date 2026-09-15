using System.Drawing;

namespace KeyboardSwitcher;

public sealed class ShortcutHintForm : Form
{
    public ShortcutHintForm()
    {
        Text = "KeyboardSwitcher";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        ControlBox = false;
        BackColor = Color.FromArgb(255, 249, 196);
        ClientSize = new Size(280, 145);

        var title = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(16, 14),
            Text = "KeyboardSwitcher"
        };

        var shortcuts = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 11),
            Location = new Point(16, 44),
            Text =
                "CapsLock + A  →  English\r\n" +
                "CapsLock + Z  →  Russian\r\n" +
                "CapsLock + Q  →  Ukrainian"
        };

        Controls.Add(title);
        Controls.Add(shortcuts);
    }


    public void ShowNearBottomRight()
    {
        Rectangle area = Screen.PrimaryScreen!.WorkingArea;

        Location = new Point(
            area.Right - Width - 24,
            area.Bottom - Height - 24);

        Show();
        BringToFront();
    }
}
