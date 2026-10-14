using CloudConverter.App.Services;
using CloudConverter.App.UI;
using CloudConverter.Core;

namespace CloudConverter.App.Forms;

internal enum CloudBrowserMode
{
    /// <summary>대상 파일 선택: 파일/폴더 다중 선택</summary>
    SelectItems,
    /// <summary>저장(업로드) 위치 선택: 폴더 1개, 다중 선택 불가</summary>
    SelectFolder,
}

/// <summary>OneDrive / SharePoint 탐색 팝업 (오프라인 시연용 가상 클라우드)</summary>
internal sealed class CloudBrowserForm : ThemedForm
{
    private readonly AppServices _services;
    private readonly LocationKind _kind;
    private readonly CloudBrowserMode _mode;
    private readonly ImageList _icons = new() { ColorDepth = ColorDepth.Depth32Bit };
    private readonly ShellIcons _shellIcons;
    private readonly ListView _list = new()
    {
        View = View.Details, FullRowSelect = true, HideSelection = false,
        HeaderStyle = ColumnHeaderStyle.Nonclickable, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle,
    };
    private readonly Label _pathLabel = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly FlatButton _backButton = new("◀") { MinimumSize = new Size(32, 26), Width = 32 };
    private readonly FlatButton _upButton = new("▲") { MinimumSize = new Size(32, 26), Width = 32 };
    private readonly FlatButton _selectButton = new("선택") { Width = 80 };
    private readonly FlatButton _cancelButton = new("취소") { Width = 80 };
    private readonly Label _hint = new() { AutoSize = true, ForeColor = Theme.Warning };
    private readonly Stack<string?> _history = new();

    /// <summary>null = SharePoint 사이트 목록</summary>
    private string? _current;

    public List<string> SelectedPaths { get; } = new();

    public CloudBrowserForm(AppServices services, LocationKind kind, CloudBrowserMode mode)
    {
        _services = services;
        _kind = kind;
        _mode = mode;
        _shellIcons = new ShellIcons(_icons);

        Text = mode == CloudBrowserMode.SelectFolder ? "[업로드 위치 선택]" : "[대상 파일 선택]";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(640, 440);
        MinimumSize = new Size(480, 320);
        ShowInTaskbar = false;
        MinimizeBox = false;
        BackColor = Color.White;

        _icons.ImageSize = new Size(Px(16), Px(16));
        _list.SmallImageList = _icons;
        _list.MultiSelect = mode == CloudBrowserMode.SelectItems;

        var nav = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _backButton.Margin = new Padding(0, 0, 4, 0);
        _upButton.Margin = new Padding(0, 0, 8, 0);
        nav.Controls.Add(_backButton, 0, 0);
        nav.Controls.Add(_upButton, 1, 0);
        nav.Controls.Add(_pathLabel, 2, 0);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _hint.Anchor = AnchorStyles.Left;
        _hint.Text = mode == CloudBrowserMode.SelectFolder ? "다중선택 불가" : "다중선택 가능 (Ctrl / Shift)";
        _hint.ForeColor = mode == CloudBrowserMode.SelectFolder ? Theme.Warning : ColorTranslator.FromHtml("#107C10");
        _selectButton.Anchor = AnchorStyles.Right;
        _cancelButton.Anchor = AnchorStyles.Right;
        _selectButton.Margin = new Padding(0, 0, 6, 0);
        _cancelButton.Margin = Padding.Empty;
        bottom.Controls.Add(_hint, 0, 0);
        bottom.Controls.Add(_selectButton, 1, 0);
        bottom.Controls.Add(_cancelButton, 2, 0);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.Controls.Add(nav, 0, 0);
        root.Controls.Add(_list, 0, 1);
        root.Controls.Add(bottom, 0, 2);
        Controls.Add(root);

        _backButton.Click += (_, _) => { if (_history.Count > 0) Navigate(_history.Pop(), remember: false); };
        _upButton.Click += (_, _) => NavigateUp();
        _list.DoubleClick += (_, _) => OpenSelected();
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { OpenSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.Back) NavigateUp();
        };
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _list.Resize += (_, _) => LayoutColumns();
        _selectButton.Click += (_, _) => Accept();
        _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        CancelButton = _cancelButton;

        Navigate(kind == LocationKind.OneDrive ? _services.Cloud.OneDriveRoot : null, remember: false);
    }

    private string RootPath => _services.Cloud.RootFor(_kind);
    private bool AtSiteList => _kind == LocationKind.SharePoint && _current is null;

    private void Navigate(string? path, bool remember)
    {
        if (remember) _history.Push(_current);
        _current = path;
        _list.BeginUpdate();
        _list.Items.Clear();
        _list.Columns.Clear();

        if (AtSiteList)
        {
            _list.Columns.Add("이름", 200);
            _list.Columns.Add("사이트 유형", 100);
            _list.Columns.Add("설명", 260);
            foreach (var site in _services.Cloud.Sites)
            {
                var full = _services.Cloud.SharePointSitePath(site.Name);
                var item = new ListViewItem(site.Name, _shellIcons.KeyFor(full, true)) { Tag = full };
                item.SubItems.Add("SharePoint");
                item.SubItems.Add(site.Description);
                _list.Items.Add(item);
            }
            _pathLabel.Text = "SharePoint";
        }
        else if (_current is not null)
        {
            _list.Columns.Add("이름", 260);
            _list.Columns.Add("수정한 날짜", 140);
            _list.Columns.Add("크기", 80, HorizontalAlignment.Right);
            try
            {
                var dir = new DirectoryInfo(_current);
                foreach (var sub in dir.EnumerateDirectories().OrderBy(d => d.Name))
                {
                    var item = new ListViewItem(sub.Name, _shellIcons.KeyFor(sub.FullName, true)) { Tag = sub.FullName };
                    item.SubItems.Add(sub.LastWriteTime.ToString("yyyy-MM-dd tt h:mm"));
                    item.SubItems.Add("");
                    _list.Items.Add(item);
                }
                if (_mode == CloudBrowserMode.SelectItems)
                {
                    foreach (var file in dir.EnumerateFiles().Where(f => !f.Name.EndsWith(".ccdemo.tmp")).OrderBy(f => f.Name))
                    {
                        var item = new ListViewItem(file.Name, _shellIcons.KeyFor(file.FullName, false)) { Tag = file.FullName };
                        item.SubItems.Add(file.LastWriteTime.ToString("yyyy-MM-dd tt h:mm"));
                        item.SubItems.Add(SizeFormat.Format(file.Length));
                        _list.Items.Add(item);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            _pathLabel.Text = _services.Cloud.ToDisplay(_current);
        }
        _list.EndUpdate();
        LayoutColumns();
        UpdateButtons();
    }

    private void LayoutColumns()
    {
        if (_list.Columns.Count == 0) return;
        var width = _list.ClientSize.Width;
        var fixedWidth = 0;
        for (var i = 1; i < _list.Columns.Count; i++)
        {
            var logical = AtSiteList ? (i == 1 ? 100 : 260) : (i == 1 ? 150 : 80);
            _list.Columns[i].Width = Px(logical);
            fixedWidth += _list.Columns[i].Width;
        }
        _list.Columns[0].Width = Math.Max(Px(160), width - fixedWidth);
    }

    private void NavigateUp()
    {
        if (_current is null) return;
        var full = Path.GetFullPath(_current).TrimEnd('\\');
        var root = Path.GetFullPath(RootPath).TrimEnd('\\');
        if (_kind == LocationKind.SharePoint)
        {
            var parent = Path.GetDirectoryName(full);
            if (parent is null || string.Equals(parent.TrimEnd('\\'), root, StringComparison.OrdinalIgnoreCase))
                Navigate(null, remember: true);
            else
                Navigate(parent, remember: true);
            return;
        }
        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase)) return;
        Navigate(Path.GetDirectoryName(full), remember: true);
    }

    private void OpenSelected()
    {
        if (_list.SelectedItems.Count != 1 || _list.SelectedItems[0].Tag is not string path) return;
        if (Directory.Exists(path))
            Navigate(path, remember: true);
        else if (_mode == CloudBrowserMode.SelectItems)
            Accept();
    }

    private void UpdateButtons()
    {
        _upButton.Enabled = _current is not null
            && !(_kind == LocationKind.OneDrive && string.Equals(Path.GetFullPath(_current).TrimEnd('\\'),
                Path.GetFullPath(RootPath).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        _backButton.Enabled = _history.Count > 0;
        _selectButton.Enabled = _mode == CloudBrowserMode.SelectFolder
            ? _current is not null || _list.SelectedItems.Count == 1
            : _list.SelectedItems.Count > 0 && !AtSiteList;
    }

    private void Accept()
    {
        SelectedPaths.Clear();
        if (_mode == CloudBrowserMode.SelectFolder)
        {
            // 목록에서 폴더를 골랐으면 그 폴더, 아니면 현재 폴더
            var chosen = _list.SelectedItems.Count == 1 && _list.SelectedItems[0].Tag is string p && Directory.Exists(p) ? p : _current;
            if (chosen is null) return;
            SelectedPaths.Add(chosen);
        }
        else
        {
            if (AtSiteList) return;
            SelectedPaths.AddRange(_list.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag!));
        }
        if (SelectedPaths.Count == 0) return;
        DialogResult = DialogResult.OK;
        Close();
    }
}
