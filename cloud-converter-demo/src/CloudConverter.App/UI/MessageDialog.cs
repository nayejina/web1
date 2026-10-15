namespace CloudConverter.App.UI;

/// <summary>기획서의 확인/알림 팝업 (가운데 정렬 메시지 + 확인/취소)</summary>
internal sealed class MessageDialog : ThemedForm
{
    private MessageDialog(string message, string title, bool withCancel)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(24, 20, 24, 16);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Fill,
        };

        var label = new Label
        {
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            MinimumSize = new Size(300, 0),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 4, 0, 18),
        };
        layout.Controls.Add(label, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Anchor = AnchorStyles.None,
            WrapContents = false,
            Margin = new Padding(0),
        };
        var ok = new FlatButton("확인") { Width = 110, DialogResult = DialogResult.OK, Margin = new Padding(6, 0, 6, 0) };
        buttons.Controls.Add(ok);
        AcceptButton = ok;
        if (withCancel)
        {
            var cancel = new FlatButton("취소") { Width = 110, DialogResult = DialogResult.Cancel, Margin = new Padding(6, 0, 6, 0) };
            buttons.Controls.Add(cancel);
            CancelButton = cancel;
        }
        else
        {
            CancelButton = ok;
        }
        layout.Controls.Add(buttons, 0, 1);
        Controls.Add(layout);
    }

    public static bool Confirm(IWin32Window? owner, string message, string title = "CloudConverter")
    {
        using var dialog = new MessageDialog(message, title, withCancel: true);
        if (owner is null) dialog.StartPosition = FormStartPosition.CenterScreen;
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    public static void Info(IWin32Window? owner, string message, string title = "CloudConverter")
    {
        using var dialog = new MessageDialog(message, title, withCancel: false);
        if (owner is null) dialog.StartPosition = FormStartPosition.CenterScreen;
        dialog.ShowDialog(owner);
    }
}
