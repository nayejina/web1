using System.Collections.Concurrent;
using System.Diagnostics;
using CloudConverter.App.Services;
using CloudConverter.App.UI;
using CloudConverter.Core;

namespace CloudConverter.App.Forms;

/// <summary>CloudConverter 메인 창 (기획서 V1.0 ~ V1.4)</summary>
internal sealed partial class MainForm : ThemedForm
{
    private sealed record LocationOption(LocationKind Kind)
    {
        public override string ToString() => Kind.DisplayName();
    }

    private readonly AppServices _services;
    private readonly ConcurrentQueue<LaunchRequest> _pendingRequests = new();
    private readonly ImageList _icons = new() { ColorDepth = ColorDepth.Depth32Bit };
    private readonly ShellIcons _shellIcons;
    private readonly ToolTip _tip = new() { InitialDelay = 300, ReshowDelay = 100, AutoPopDelay = 8000 };

    // 헤더
    private readonly Label _formatLabel = new() { Text = "변환할 형식", AutoSize = true, ForeColor = Theme.HeaderText, BackColor = Theme.HeaderBack };
    private readonly HeaderRadio _mipRadio = new(ConversionFormat.Mip.DisplayName());
    private readonly HeaderRadio _drmRadio = new(ConversionFormat.Drm.DisplayName());
    private readonly InfoButton _infoButton = new();
    private readonly FlatButton _startButton = new("변환 시작", FlatButtonKind.Primary) { Width = 96 };

    // 대상 파일/폴더
    private readonly Label _targetLabel = new() { Text = "대상파일/폴더", AutoSize = true };
    private readonly ComboBox _targetCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly FlatButton _targetFind = new("찾기");
    private readonly ListView _targetList = new()
    {
        View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = true,
        HeaderStyle = ColumnHeaderStyle.Nonclickable, BorderStyle = BorderStyle.FixedSingle, AllowDrop = true,
        Dock = DockStyle.Fill, BackColor = Theme.ListBack, ShowItemToolTips = false,
    };
    private readonly ContextMenuStrip _targetMenu = new();
    private readonly ContextMenuStrip _findMenu = new();

    // 저장 위치
    private readonly Label _saveLabel = new() { Text = "저장 위치", AutoSize = true };
    private readonly ComboBox _saveCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly FlatButton _saveFind = new("찾기");
    private readonly TextBox _savePathBox = new() { ReadOnly = true, Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
    private readonly CheckBox _sameFolderCheck = new() { Text = "대상 파일과 동일 폴더에 저장", AutoSize = true };

    // 변환 상태
    private readonly Label _statusLabel = new() { Text = "변환 상태", AutoSize = true };
    private readonly FlatButton _logButton = new("로그 보기") { Width = 86 };
    private readonly ListView _statusList = new()
    {
        View = View.Details, FullRowSelect = true, MultiSelect = false, OwnerDraw = true,
        HeaderStyle = ColumnHeaderStyle.Nonclickable, BorderStyle = BorderStyle.FixedSingle,
        Dock = DockStyle.Fill, BackColor = Theme.ListBack,
    };

    // 하단
    private readonly FlatButton _resetButton = new("선택 옵션 초기화") { Width = 120 };
    private readonly FlatButton _closeButton = new("닫기") { Width = 86 };

    private ConversionFormat? _format;
    private string? _savePath;
    private bool _running;
    private bool _stopRequested;
    private bool _closeAfterStop;
    private bool _suppressComboEvents;
    private LogViewerForm? _logViewer;
    private (ListView list, int item, int column)? _tipTarget;

    public MainForm(AppServices services, LaunchRequest? initialRequest)
    {
        _services = services;
        _shellIcons = new ShellIcons(_icons);
        if (initialRequest is not null)
            _pendingRequests.Enqueue(initialRequest);

        Text = services.Settings.WindowTitle;
        StartPosition = FormStartPosition.CenterScreen;
        // 디자인 가이드(1): 기본 1050 * 632, 최소 859 * 461, 최대 제한 없음
        Size = new Size(1050, 632);
        MinimumSize = new Size(859, 461);
        KeyPreview = true;

        BuildLayout();
        WireEvents();
        ResetAll();
    }

    // ───────────────────────────── 레이아웃 ─────────────────────────────

    private void BuildLayout()
    {
        SuspendLayout();
        _icons.ImageSize = new Size(Px(16), Px(16));
        _targetList.SmallImageList = _icons;
        _statusList.SmallImageList = _icons;
        // 행 높이 확보용 (아이콘 크기가 행 높이를 결정)
        _targetList.Columns.Add("파일/폴더명");
        _targetList.Columns.Add("크기", 80, HorizontalAlignment.Right);
        _targetList.Columns.Add("암호화", 64, HorizontalAlignment.Center);
        _targetList.Columns.Add("경로", 260);
        _statusList.Columns.Add("파일명");
        _statusList.Columns.Add("진행 상태", 120, HorizontalAlignment.Center);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, Padding = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildContent(), 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        Controls.Add(root);

        _targetMenu.Items.Add("목록에서 제외", null, (_, _) => RemoveSelectedTargets());
        _findMenu.Items.Add("파일 선택...", null, (_, _) => BrowseLocalFiles());
        _findMenu.Items.Add("폴더 선택...", null, (_, _) => BrowseLocalFolder());
        _targetList.ContextMenuStrip = _targetMenu;
        ResumeLayout(true);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = Theme.HeaderBack, ColumnCount = 6, RowCount = 1,
            Padding = new Padding(12, 0, 12, 0), Margin = Padding.Empty,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _formatLabel.Anchor = AnchorStyles.Left;
        _formatLabel.Margin = new Padding(0, 0, 14, 0);
        foreach (var radio in new[] { _mipRadio, _drmRadio })
        {
            radio.Anchor = AnchorStyles.Left;
            radio.Size = new Size(TextRenderer.MeasureText(radio.Text, Font).Width + 30, 24);
            radio.Margin = new Padding(10, 0, 10, 0);
        }
        _infoButton.Anchor = AnchorStyles.Right;
        _startButton.Anchor = AnchorStyles.Right;
        _startButton.Margin = Padding.Empty;

        header.Controls.Add(_formatLabel, 0, 0);
        header.Controls.Add(_mipRadio, 1, 0);
        header.Controls.Add(_drmRadio, 2, 0);
        header.Controls.Add(_infoButton, 4, 0);
        header.Controls.Add(_startButton, 5, 0);
        _tip.SetToolTip(_infoButton, "프로그램 정보");
        return header;
    }

    private Control BuildContent()
    {
        // 디자인 가이드(1): 좌(대상) : 우(저장/상태) = 6 : 4, 사이 여백 10px
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(10, 10, 10, 0), Margin = Padding.Empty,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 5, 0) };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(BuildSectionHeader(_targetLabel, _targetCombo, _targetFind), 0, 0);
        left.Controls.Add(_targetList, 0, 1);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = new Padding(5, 0, 0, 0) };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Controls.Add(BuildSectionHeader(_saveLabel, _saveCombo, _saveFind), 0, 0);
        _savePathBox.Margin = new Padding(0, 2, 0, 2);
        right.Controls.Add(_savePathBox, 0, 1);
        _sameFolderCheck.Anchor = AnchorStyles.Left;
        _sameFolderCheck.Margin = new Padding(2, 0, 0, 0);
        right.Controls.Add(_sameFolderCheck, 0, 2);
        right.Controls.Add(BuildSectionHeader(_statusLabel, null, _logButton), 0, 3);
        right.Controls.Add(_statusList, 0, 4);

        content.Controls.Add(left, 0, 0);
        content.Controls.Add(right, 1, 0);
        return content;
    }

    private static Control BuildSectionHeader(Label label, ComboBox? combo, Button button)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        label.Anchor = AnchorStyles.Left;
        label.Margin = new Padding(0);
        panel.Controls.Add(label, 0, 0);
        if (combo is not null)
        {
            combo.Anchor = AnchorStyles.Right;
            combo.Margin = new Padding(0, 0, 6, 0);
            panel.Controls.Add(combo, 1, 0);
        }
        button.Anchor = AnchorStyles.Right;
        button.Margin = Padding.Empty;
        panel.Controls.Add(button, 2, 0);
        return panel;
    }

    private Control BuildFooter()
    {
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(10, 8, 10, 10), Margin = Padding.Empty };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _resetButton.Anchor = AnchorStyles.Left;
        _resetButton.Margin = Padding.Empty;
        _closeButton.Anchor = AnchorStyles.Right;
        _closeButton.Margin = Padding.Empty;
        footer.Controls.Add(_resetButton, 0, 0);
        footer.Controls.Add(_closeButton, 1, 0);
        return footer;
    }

    private void LayoutColumns()
    {
        // 디자인 가이드(1)-2: 크기/암호화 컬럼은 우측 고정, 창 크기 조절 시 파일/폴더명 컬럼 크기가 변경
        // V1.2: 경로는 가장 뒤 컬럼. 파일명을 우선 표시하고 좁아지면 경로는 뒤로 스크롤
        var t = _targetList.ClientSize.Width - Px(2);
        int size = Px(76), enc = Px(64), path = Px(240);
        _targetList.Columns[1].Width = size;
        _targetList.Columns[2].Width = enc;
        var name = Math.Max(Px(180), t - size - enc - path);
        _targetList.Columns[0].Width = name;
        _targetList.Columns[3].Width = Math.Max(path, t - name - size - enc);

        // 파일명 : 진행 상태 = 2 : 1, 진행 상태 우측 고정
        var s = _statusList.ClientSize.Width - Px(2);
        var status = Math.Max(Px(100), s / 3);
        _statusList.Columns[1].Width = status;
        _statusList.Columns[0].Width = Math.Max(Px(120), s - status);
    }

    // ───────────────────────────── 이벤트 연결 ─────────────────────────────

    private void WireEvents()
    {
        _mipRadio.CheckedChanged += (_, _) => { if (_mipRadio.Checked) SetFormat(ConversionFormat.Mip); };
        _drmRadio.CheckedChanged += (_, _) => { if (_drmRadio.Checked) SetFormat(ConversionFormat.Drm); };
        _infoButton.Click += (_, _) => { using var about = new AboutForm(_services); about.ShowDialog(this); };
        _startButton.Click += (_, _) => { if (_running) RequestStop(); else StartConversion(); };

        _targetCombo.SelectedIndexChanged += (_, _) => { if (!_suppressComboEvents) OnTargetLocationChanged(); };
        _saveCombo.SelectedIndexChanged += (_, _) => { if (!_suppressComboEvents) OnSaveLocationChanged(); };
        _targetFind.Click += (_, _) => BrowseTargets();
        _saveFind.Click += (_, _) => BrowseSaveLocation();
        _sameFolderCheck.CheckedChanged += (_, _) => UpdateUiState();
        _logButton.Click += (_, _) => ShowLogViewer();
        _resetButton.Click += (_, _) =>
        {
            if (MessageDialog.Confirm(this, "선택한 모든 옵션이 초기화됩니다.\n계속하시겠습니까?"))
                ResetAll();
        };
        _closeButton.Click += (_, _) => Close();

        _targetList.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) RemoveSelectedTargets(); };
        _targetList.DragEnter += OnTargetDragEnter;
        _targetList.DragOver += OnTargetDragEnter;
        _targetList.DragDrop += OnTargetDragDrop;
        _targetList.MouseMove += (_, e) => UpdateListTooltip(_targetList, e.Location);
        _targetList.MouseLeave += (_, _) => HideListTooltip();
        _targetList.Resize += (_, _) => LayoutColumns();
        _targetMenu.Opening += (_, e) =>
        {
            _targetMenu.Items[0].Enabled = !_running && _targetList.SelectedItems.Count > 0;
            if (_targetList.SelectedItems.Count == 0) e.Cancel = true;
        };

        _statusList.DrawColumnHeader += (_, e) => e.DrawDefault = true;
        _statusList.DrawItem += (_, _) => { };
        _statusList.DrawSubItem += DrawStatusSubItem;
        _statusList.MouseMove += (_, e) => UpdateListTooltip(_statusList, e.Location);
        _statusList.MouseLeave += (_, _) => HideListTooltip();
        _statusList.Resize += (_, _) => LayoutColumns();
        _statusList.DoubleClick += (_, _) => RevealSelectedOutput();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        LayoutColumns();
        if (_services.ConfigErrors.Count > 0)
            MessageDialog.Info(this, "일부 설정을 읽지 못해 기본값을 사용합니다.\n\n" + string.Join("\n", _services.ConfigErrors));
        ProcessPendingRequests();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_running && !_closeAfterStop)
        {
            e.Cancel = true;
            if (MessageDialog.Confirm(this, "파일 작업이 진행중입니다. 종료하시겠습니까?\n종료하더라도 이미 변환된 파일은 변환된 상태로 유지됩니다.", "알림"))
            {
                _closeAfterStop = true;
                _stopRequested = true;
            }
            return;
        }
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5 && _logViewer is { IsDisposed: false })
            _logViewer.ReloadEntries();
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ───────────────────────────── 형식 / 위치 선택 ─────────────────────────────

    private void SetFormat(ConversionFormat format)
    {
        if (_format == format) return;
        _format = format;
        if (format == ConversionFormat.Mip && !_mipRadio.Checked) _mipRadio.Checked = true;
        if (format == ConversionFormat.Drm && !_drmRadio.Checked) _drmRadio.Checked = true;
        RefreshTargetCombo();
        RefreshSaveCombo();
        UpdateUiState();
    }

    private LocationKind TargetKind => (_targetCombo.SelectedItem as LocationOption)?.Kind ?? LocationKind.MyDrive;
    private LocationKind SaveKind => (_saveCombo.SelectedItem as LocationOption)?.Kind ?? LocationKind.MyDrive;

    /// <summary>대상 위치 목록: 형식별 정책. 저장 위치를 먼저 골랐다면 그와 매칭되는 항목만</summary>
    private void RefreshTargetCombo()
    {
        var kinds = _format is { } f
            ? (_savePath is not null && _targetList.Items.Count == 0
                ? _services.Policy.SourcesForTarget(f, SaveKind)
                : _services.Policy.SourcesFor(f))
            : new[] { LocationKind.MyDrive };
        SetComboItems(_targetCombo, kinds, TargetKind);
    }

    /// <summary>저장 위치 목록: 선택된 대상 위치와 매칭되는 항목만</summary>
    private void RefreshSaveCombo()
    {
        var kinds = _format is { } f ? _services.Policy.TargetsFor(f, TargetKind) : new[] { LocationKind.MyDrive };
        var previous = SaveKind;
        SetComboItems(_saveCombo, kinds, previous);
        if (_savePath is not null && SaveKind != previous)
            ClearSavePath();
    }

    private void SetComboItems(ComboBox combo, IReadOnlyList<LocationKind> kinds, LocationKind preferred)
    {
        _suppressComboEvents = true;
        try
        {
            combo.BeginUpdate();
            combo.Items.Clear();
            foreach (var kind in kinds)
                combo.Items.Add(new LocationOption(kind));
            if (combo.Items.Count > 0)
            {
                var index = kinds.ToList().IndexOf(preferred);
                combo.SelectedIndex = index >= 0 ? index : 0;
            }
            combo.EndUpdate();
        }
        finally
        {
            _suppressComboEvents = false;
        }
    }

    private void OnTargetLocationChanged()
    {
        RefreshSaveCombo();
        UpdateUiState();
    }

    private void OnSaveLocationChanged()
    {
        ClearSavePath();
        RefreshTargetCombo();
        UpdateUiState();
    }

    private void ClearSavePath()
    {
        _savePath = null;
        _savePathBox.Text = "";
    }

    // ───────────────────────────── 활성화 / 비활성화 정책 ─────────────────────────────

    private int ConvertibleFileCount => TargetItems.Count(t => !t.IsFolder && t.Eligibility.Convertible);
    private int FolderCount => TargetItems.Count(t => t.IsFolder);
    private IEnumerable<TargetItem> TargetItems => _targetList.Items.Cast<ListViewItem>().Select(i => (TargetItem)i.Tag!);

    private bool SameFolderPossible =>
        _format is { } f && _targetList.Items.Count > 0
        && TargetKind == LocationKind.MyDrive && SaveKind == LocationKind.MyDrive
        && _services.Policy.IsAllowed(f, LocationKind.MyDrive, LocationKind.MyDrive);

    /// <summary>기획서 13p 활성화/비활성화 정책</summary>
    private void UpdateUiState()
    {
        var hasFormat = _format is not null;
        var idle = !_running;

        if (!SameFolderPossible && _sameFolderCheck.Checked)
            _sameFolderCheck.Checked = false;
        var sameFolder = _sameFolderCheck.Checked;

        _mipRadio.Enabled = idle;
        _drmRadio.Enabled = idle;

        _targetLabel.ForeColor = hasFormat ? ForeColor : Theme.LabelDisabled;
        _targetCombo.Enabled = hasFormat && idle && _targetList.Items.Count == 0;
        _targetFind.Enabled = hasFormat && idle;
        _targetList.Enabled = hasFormat;
        _targetList.AllowDrop = hasFormat && idle;

        _saveLabel.ForeColor = hasFormat ? ForeColor : Theme.LabelDisabled;
        _saveCombo.Enabled = hasFormat && idle && !sameFolder;
        _saveFind.Enabled = hasFormat && idle && !sameFolder;
        _savePathBox.Enabled = hasFormat && !sameFolder;
        _sameFolderCheck.Enabled = idle && SameFolderPossible;

        _statusLabel.ForeColor = hasFormat ? ForeColor : Theme.LabelDisabled;
        _statusList.Enabled = hasFormat || _statusList.Items.Count > 0;

        _startButton.Text = _running ? "작업 중지" : "변환 시작";
        _startButton.Enabled = _running
            || (hasFormat && (ConvertibleFileCount > 0 || FolderCount > 0) && (sameFolder || _savePath is not null));
        _resetButton.Enabled = idle;
    }

    private void ResetAll()
    {
        _format = null;
        _mipRadio.Checked = false;
        _drmRadio.Checked = false;
        _targetList.Items.Clear();
        _statusList.Items.Clear();
        _sameFolderCheck.Checked = false;
        ClearSavePath();
        RefreshTargetCombo();
        RefreshSaveCombo();
        UpdateUiState();
    }

    // ───────────────────────────── 외부(우클릭 메뉴) 요청 ─────────────────────────────

    /// <summary>파이프 스레드에서 호출됨</summary>
    public void EnqueueExternalRequest(LaunchRequest request)
    {
        _pendingRequests.Enqueue(request);
        if (IsHandleCreated)
            BeginInvoke(ProcessPendingRequests);
    }

    private void ProcessPendingRequests()
    {
        var any = false;
        while (_pendingRequests.TryDequeue(out var request))
        {
            any = true;
            if (request.Format is not { } format || request.Paths.Count == 0)
                continue;
            if (_running)
            {
                MessageDialog.Info(this, "변환 작업 중에는 파일을 추가할 수 없습니다.");
                continue;
            }
            // 우클릭 메뉴(MIP로 변환 / DRM으로 변환)로 들어온 경우 해당 형식이 선택된 상태로 실행
            if (_targetList.Items.Count > 0 && TargetKind != LocationKind.MyDrive)
                _targetList.Items.Clear();
            SetFormat(format);
            SelectTargetKind(LocationKind.MyDrive);
            AddTargets(request.Paths, LocationKind.MyDrive);
        }
        if (any) BringToFrontForce();
    }

    private void BringToFrontForce()
    {
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        TopMost = true;
        TopMost = false;
        Activate();
    }

    private bool SelectTargetKind(LocationKind kind)
    {
        if (TargetKind == kind) return true;
        for (var i = 0; i < _targetCombo.Items.Count; i++)
        {
            if (_targetCombo.Items[i] is LocationOption option && option.Kind == kind)
            {
                _targetCombo.SelectedIndex = i;
                return true;
            }
        }
        // 저장 위치 때문에 목록이 줄어 있으면 전체 목록으로 되돌린 뒤 선택
        ClearSavePath();
        RefreshTargetCombo();
        for (var i = 0; i < _targetCombo.Items.Count; i++)
        {
            if (_targetCombo.Items[i] is LocationOption option && option.Kind == kind)
            {
                _targetCombo.SelectedIndex = i;
                return true;
            }
        }
        return false;
    }

    // ───────────────────────────── 대상 파일 추가 / 제외 ─────────────────────────────

    private void BrowseTargets()
    {
        if (_format is null) return;
        if (TargetKind == LocationKind.MyDrive)
        {
            _findMenu.Show(_targetFind, new Point(0, _targetFind.Height));
            return;
        }
        using var dialog = new CloudBrowserForm(_services, TargetKind, CloudBrowserMode.SelectItems);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddTargets(dialog.SelectedPaths, TargetKind);
    }

    private void BrowseLocalFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "대상 파일 선택",
            Multiselect = true,
            Filter = "변환 가능 문서|" + string.Join(";", _services.Rules.AllowedExtensions.Select(e => "*" + e)) + "|모든 파일|*.*",
            FilterIndex = 2,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddTargets(dialog.FileNames, LocationKind.MyDrive);
    }

    private void BrowseLocalFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "대상 폴더 선택", UseDescriptionForTitle = true, ShowNewFolderButton = false };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddTargets(new[] { dialog.SelectedPath }, LocationKind.MyDrive);
    }

    private void AddTargets(IEnumerable<string> paths, LocationKind kind)
    {
        if (_targetList.Items.Count > 0 && TargetKind != kind)
        {
            MessageDialog.Info(this, $"대상 파일은 한 위치에서만 선택할 수 있습니다.\n(현재: {TargetKind.DisplayName()})");
            return;
        }
        var existing = new HashSet<string>(TargetItems.Select(t => t.FullPath), StringComparer.OrdinalIgnoreCase);
        _targetList.BeginUpdate();
        foreach (var raw in paths)
        {
            string full;
            try { full = Path.GetFullPath(raw); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { continue; }
            var isFolder = Directory.Exists(full);
            if ((!isFolder && !File.Exists(full)) || !existing.Add(full))
                continue;

            var item = new TargetItem
            {
                FullPath = full,
                IsFolder = isFolder,
                Size = isFolder ? 0 : new FileInfo(full).Length,
                Eligibility = isFolder ? new Eligibility(true, 0) : _services.Engine.CheckEligibility(full),
                Location = kind,
                DisplayPath = kind == LocationKind.MyDrive ? full : _services.Cloud.ToDisplay(full),
            };
            var row = new ListViewItem(item.Name, _shellIcons.KeyFor(full, isFolder)) { Tag = item };
            row.SubItems.Add(isFolder ? "<폴더>" : SizeFormat.Format(item.Size));
            // V1.0: 폴더는 가능 여부 표시 X
            row.SubItems.Add(isFolder ? "" : item.Eligibility.Convertible ? "가능" : "불가");
            row.SubItems.Add(item.DisplayPath);
            if (!isFolder && !item.Eligibility.Convertible)
                row.SubItems[2].ForeColor = Theme.Warning;
            row.UseItemStyleForSubItems = false;
            _targetList.Items.Add(row);
        }
        _targetList.EndUpdate();
        RefreshTargetCombo();
        UpdateUiState();
    }

    private void RemoveSelectedTargets()
    {
        if (_running || _targetList.SelectedItems.Count == 0) return;
        _targetList.BeginUpdate();
        foreach (ListViewItem item in _targetList.SelectedItems.Cast<ListViewItem>().ToList())
            _targetList.Items.Remove(item);
        _targetList.EndUpdate();
        RefreshTargetCombo();
        UpdateUiState();
    }

    private void OnTargetDragEnter(object? sender, DragEventArgs e)
    {
        var ok = _format is not null && !_running && e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                 && (_targetList.Items.Count == 0 || TargetKind == LocationKind.MyDrive);
        e.Effect = ok ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnTargetDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths) return;
        if (_targetList.Items.Count == 0 && !SelectTargetKind(LocationKind.MyDrive))
        {
            MessageDialog.Info(this, "현재 정책에서 내 드라이브 파일을 대상으로 선택할 수 없습니다.");
            return;
        }
        AddTargets(paths, LocationKind.MyDrive);
    }

    // ───────────────────────────── 저장 위치 ─────────────────────────────

    private void BrowseSaveLocation()
    {
        if (_format is null) return;
        if (SaveKind == LocationKind.MyDrive)
        {
            using var dialog = new FolderBrowserDialog { Description = "저장 위치 선택", UseDescriptionForTitle = true, ShowNewFolderButton = true };
            if (_savePath is not null && Directory.Exists(_savePath)) dialog.InitialDirectory = _savePath;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            _savePath = dialog.SelectedPath;
            _savePathBox.Text = _savePath;
        }
        else
        {
            using var dialog = new CloudBrowserForm(_services, SaveKind, CloudBrowserMode.SelectFolder);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedPaths.Count == 0) return;
            _savePath = dialog.SelectedPaths[0];
            _savePathBox.Text = _services.Cloud.ToDisplay(_savePath);
        }
        RefreshTargetCombo();
        UpdateUiState();
    }

    // ───────────────────────────── 로그 보기 ─────────────────────────────

    private void ShowLogViewer()
    {
        if (_logViewer is { IsDisposed: false })
        {
            _logViewer.ReloadEntries();
            _logViewer.Activate();
            return;
        }
        _logViewer = new LogViewerForm(_services) { StartPosition = FormStartPosition.Manual };
        var area = Screen.FromControl(this).WorkingArea;
        var x = Left + (Width - _logViewer.Width) / 2;
        var y = Top + (Height - _logViewer.Height) / 2;
        _logViewer.Location = new Point(
            Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - _logViewer.Width)),
            Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - _logViewer.Height)));
        _logViewer.Show(this);
    }

    // ───────────────────────────── 툴팁 ─────────────────────────────

    private void UpdateListTooltip(ListView list, Point location)
    {
        var hit = list.HitTest(location);
        if (hit.Item is null || hit.SubItem is null)
        {
            HideListTooltip();
            return;
        }
        var column = hit.Item.SubItems.IndexOf(hit.SubItem);
        var key = (list, hit.Item.Index, column);
        if (_tipTarget == key) return;
        _tipTarget = key;

        string? text = null;
        if (list == _targetList && hit.Item.Tag is TargetItem target)
        {
            // 롤오버 툴팁(1): 전체 경로 / (2): 암호화 불가 사유
            if (column == 0 || column == 3) text = target.DisplayPath;
            else if (column == 2 && !target.IsFolder && !target.Eligibility.Convertible)
                text = _services.Classification.Rule(target.Eligibility.ReasonCode).Reason;
        }
        else if (list == _statusList && hit.Item.Tag is StatusRow row)
        {
            // 롤오버 툴팁(3): 실패(스킵) 사유
            if (column == 1 && row.Style is StatusStyle.Fail or StatusStyle.Skip) text = row.Reason;
            else if (column == 0 && row.OutputPath is not null) text = "저장 위치: " + DisplayOf(row.OutputPath);
        }

        if (string.IsNullOrEmpty(text))
        {
            _tip.Hide(list);
            return;
        }
        _tip.Show(text, list, location.X + Px(12), location.Y + Px(18), 6000);
    }

    private void HideListTooltip()
    {
        _tipTarget = null;
        _tip.Hide(_targetList);
        _tip.Hide(_statusList);
    }

    private string DisplayOf(string path) => _services.Cloud.KindOf(path) is null ? path : _services.Cloud.ToDisplay(path);

    private void RevealSelectedOutput()
    {
        if (_statusList.SelectedItems.Count == 0 || _statusList.SelectedItems[0].Tag is not StatusRow { OutputPath: { } path }) return;
        if (!File.Exists(path) || _services.Cloud.KindOf(path) is not null) return;
        try { Process.Start("explorer.exe", $"/select,\"{path}\""); }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
