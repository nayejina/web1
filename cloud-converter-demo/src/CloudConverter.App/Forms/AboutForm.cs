using System.Diagnostics;
using CloudConverter.App.Services;
using CloudConverter.App.UI;
using CloudConverter.Core;

namespace CloudConverter.App.Forms;

/// <summary>프로그램 정보 (ⓘ 버튼). 하단에 시연용 안내 문구를 항상 표시한다.</summary>
internal sealed class AboutForm : ThemedForm
{
    private readonly AppServices _services;
    private readonly FlatButton _menuButton = new("") { Width = 150 };

    public AboutForm(AppServices services)
    {
        _services = services;
        var settings = services.Settings;
        Text = "프로그램 정보";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(20, 16, 20, 14);

        var info = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 3, Margin = new Padding(0, 0, 24, 0) };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddRow(info, 0, "제품명", settings.ProductName);
        AddRow(info, 1, "계정", settings.Account);
        AddRow(info, 2, "버전", settings.Version);

        var icon = new PictureBox
        {
            Image = new Icon(Theme.AppIcon, 64, 64).ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(64, 64),
            Anchor = AnchorStyles.Right,
        };

        var top = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 1, Dock = DockStyle.Fill, Margin = Padding.Empty };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.Controls.Add(info, 0, 0);
        top.Controls.Add(icon, 1, 0);

        var copyright = new Label { Text = settings.Copyright, AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 14, 0, 0) };
        var separator = new Label { BorderStyle = BorderStyle.Fixed3D, Height = 2, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 10) };
        var disclaimer = new Label
        {
            Text = DemoSettings.Disclaimer,
            AutoSize = true,
            ForeColor = Theme.Warning,
            Font = Theme.Bold(),
            MaximumSize = new Size(560, 0),
            Margin = new Padding(0, 0, 0, 12),
        };

        var buttons = new TableLayoutPanel { AutoSize = true, ColumnCount = 4, RowCount = 1, Dock = DockStyle.Fill, Margin = Padding.Empty };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var samples = new FlatButton("샘플 폴더 열기") { Width = 130, Margin = new Padding(0, 0, 6, 0) };
        _menuButton.Margin = new Padding(0, 0, 6, 0);
        var close = new FlatButton("닫기") { Width = 80, DialogResult = DialogResult.OK, Margin = Padding.Empty };
        buttons.Controls.Add(_menuButton, 0, 0);
        buttons.Controls.Add(samples, 1, 0);
        buttons.Controls.Add(close, 3, 0);
        AcceptButton = close;
        CancelButton = close;

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 5, Dock = DockStyle.Fill, MinimumSize = new Size(500, 0) };
        root.Controls.Add(top, 0, 0);
        root.Controls.Add(copyright, 0, 1);
        root.Controls.Add(separator, 0, 2);
        root.Controls.Add(disclaimer, 0, 3);
        root.Controls.Add(buttons, 0, 4);
        Controls.Add(root);

        UpdateMenuButton();
        _menuButton.Click += (_, _) => ToggleContextMenu();
        samples.Click += (_, _) =>
        {
            Directory.CreateDirectory(_services.SampleFolder);
            try { Process.Start("explorer.exe", $"\"{_services.SampleFolder}\""); }
            catch (System.ComponentModel.Win32Exception) { }
        };
    }

    private static void AddRow(TableLayoutPanel table, int row, string name, string value)
    {
        table.Controls.Add(new Label { Text = name, AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 4, 18, 4) }, 0, row);
        table.Controls.Add(new Label { Text = value, AutoSize = true, Margin = new Padding(0, 4, 0, 4) }, 1, row);
    }

    private bool Registered => ContextMenuRegistrar.IsRegistered(_services.Rules.AllowedExtensions);

    private void UpdateMenuButton()
    {
        _menuButton.Text = Registered ? "탐색기 우클릭 메뉴 해제" : "탐색기 우클릭 메뉴 등록";
        _menuButton.Width = _menuButton.PreferredWidthFor(_menuButton.Text);
    }

    private void ToggleContextMenu()
    {
        try
        {
            if (Registered)
            {
                ContextMenuRegistrar.Unregister(_services.Rules.AllowedExtensions);
                MessageDialog.Info(this, "탐색기 우클릭 메뉴를 해제했습니다.\n(다음 실행 시 자동 등록하지 않으려면 demo-settings.json 의 AutoRegisterContextMenu 를 false 로 바꾸세요.)");
            }
            else
            {
                ContextMenuRegistrar.Register(_services.Rules.AllowedExtensions);
                MessageDialog.Info(this, "탐색기 우클릭 메뉴를 등록했습니다.\nWindows 11 에서는 우클릭 후 \"더 많은 옵션 표시\"(Shift+F10)에 나타납니다.");
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            MessageDialog.Info(this, "레지스트리를 변경하지 못했습니다.\n" + ex.Message);
        }
        UpdateMenuButton();
    }
}
