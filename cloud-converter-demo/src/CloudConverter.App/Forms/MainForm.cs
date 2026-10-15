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
    private readonly Label _formatLabel = new() { Text = "변환할 형식", AutoSize = true };
    private readonly RadioButton _mipRadio = new() { Text = ConversionFormat.Mip.DisplayName(), AutoSize = true };
    private readonly RadioButton _drmRadio = new() { Text = ConversionFormat.Drm.DisplayName(), AutoSize = true };
    private readonly InfoButton _infoButton = new();
    private readonly FlatButton _startButton = new("변환 시작", FlatButtonKind.Primary);

    // 대상 파일/폴더
    private readonly Label _targetLabel = new() { Text = "대상 파일 / 폴더", AutoSize = true };
    private readonly ComboBox _targetCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly FlatButton _targetFind = new("찾기");
    private readonly ListView _targetList = new()
    {
        View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = true,
        HeaderStyle = ColumnHeaderStyle.Nonclickable, BorderStyle = BorderStyle.FixedSingle, AllowDrop = true,
        BackColor = Theme.ListBack, ShowItemToolTips = false,
    };
    private readonly ContextMenuStrip _targetMenu = new();
    private readonly ContextMenuStrip _findMenu = new();

    // 저장 위치
    private readonly Label _saveLabel = new() { Text = "저장 위치", AutoSize = true };
    private readonly ComboBox _saveCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly FlatButton _saveFind = new("찾기");
    private readonly Panel _savePathPanel = new() { BackColor = Color.White };
    private readonly TextBox _savePathBox = new() { ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.None };
    private readonly CheckBox _sameFolderCheck = new() { Text = "대상 파일과 동일 폴더에 저장", AutoSize = true };

    // 변환 상태
    private readonly Label _statusLabel = new() { Text = "변환 상태", AutoSize = true };
    private readonly FlatButton _logButton = new("로그 보기");
    private readonly ListView _statusList = new()
    {
        View = View.Details, FullRowSelect = true, MultiSelect = false, OwnerDraw = true,
        HeaderStyle = ColumnHeaderStyle.Nonclickable, BorderStyle = BorderStyle.FixedSingle,
        BackColor = Theme.ListBack,
    };

    // 하단
    private readonly FlatButton _resetButton = new("선택 옵션 초기화");
    private readonly FlatButton _closeButton = new("닫기");

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
        _shellIcons = new ShellIcons(_icons, Px(RowIconPadding));
        if (initialRequest is not null)
            _pendingRequests.Enqueue(initialRequest);

        Text = services.Settings.WindowTitle;
        StartPosition = FormStartPosition.CenterScreen;
        // 디자인 가이드(1): 기본 1050 * 632, 최대 제한 없음. 최소 너비는 오른쪽 열 폭의 2배, 최소 높이 461
        Size = new Size(1050, 632);
        MinimumSize = new Size(RightColumnWidth * 2, 461);
        KeyPreview = true;
        DoubleBuffered = true;

        BuildLayout();
        WireEvents();
        ResetAll();
    }

    // ───────────────────────────── 레이아웃 ─────────────────────────────
    // 실제 Converter 툴 화면(1050 * 632)에서 잰 값 (96 DPI 기준 px, 클라이언트 영역 좌표)

    private const int MarginLeft = 17, MarginRight = 14;
    private const int ColumnGap = 35;
    /// <summary>오른쪽 열(저장 위치/변환 상태) 기본 폭. 창을 줄이면 오른쪽은 유지하고 왼쪽 열이 줄어든다.</summary>
    private const int RightColumnWidth = 384;
    private const double RightColumnRatio = 384.0 / 968.0;
    private const int HeaderTop = 22, HeaderHeight = 32, SeparatorY = 68;
    private const int SectionTop = 85, ControlHeight = 30;
    private const int ListTop = 126, ListBottomGap = 57, FooterBottomGap = 44;
    private const int ComboWidth = 150, FindWidth = 60, ComboGap = 6;
    private const int PathTop = 127, PathHeight = 26;
    private const int CheckCenterY = 181;
    private const int StatusHeaderTop = 217, StatusHeaderHeight = 31, StatusListTop = 260;
    private const int RowHeight = 24, RowIconPadding = (RowHeight - 16) / 2;

    private void BuildLayout()
    {
        SuspendLayout();
        // 행 높이 = 이미지 높이: 16px 아이콘을 위아래 여백이 있는 24px 이미지로 넣는다
        _icons.ImageSize = new Size(Px(18), Px(RowHeight));
        _targetList.SmallImageList = _icons;
        _statusList.SmallImageList = _icons;
        _targetList.Columns.Add("파일/폴더명");
        _targetList.Columns.Add("크기", 84, HorizontalAlignment.Center);
        _targetList.Columns.Add("암호화", 89, HorizontalAlignment.Center);
        _targetList.Columns.Add("경로", 240);
        _statusList.Columns.Add("파일명");
        _statusList.Columns.Add("진행 상태", 134, HorizontalAlignment.Center);

        _savePathPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, _savePathPanel.Width - 1, _savePathPanel.Height - 1);
        };
        _savePathPanel.Resize += (_, _) => _savePathPanel.Invalidate();
        _savePathPanel.Controls.Add(_savePathBox);

        Controls.AddRange(new Control[]
        {
            _formatLabel, _mipRadio, _drmRadio, _infoButton, _startButton,
            _targetLabel, _targetCombo, _targetFind, _targetList,
            _saveLabel, _saveCombo, _saveFind, _savePathPanel, _sameFolderCheck,
            _statusLabel, _logButton, _statusList,
            _resetButton, _closeButton,
        });
        foreach (Control c in Controls) c.Margin = Padding.Empty;
        _tip.SetToolTip(_infoButton, "프로그램 정보");

        _targetMenu.Items.Add("목록에서 제외", null, (_, _) => RemoveSelectedTargets());
        _findMenu.Items.Add("파일 선택...", null, (_, _) => BrowseLocalFiles());
        _findMenu.Items.Add("폴더 선택...", null, (_, _) => BrowseLocalFolder());
        _targetList.ContextMenuStrip = _targetMenu;
        ResumeLayout(false);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        LayoutControls();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // 상단 메뉴 아래 구분선
        using var pen = new Pen(Theme.HeaderSeparator);
        var y = Px(SeparatorY);
        e.Graphics.DrawLine(pen, Px(MarginLeft), y, ClientSize.Width - Px(MarginRight), y);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    /// <summary>모든 컨트롤 배치 (96 DPI 기준 값을 현재 DPI 로 환산)</summary>
    private void LayoutControls()
    {
        if (_statusList.Columns.Count == 0) return;
        var w = ClientSize.Width;
        var h = ClientSize.Height;
        if (w <= 0 || h <= 0) return;

        var left = Px(MarginLeft);
        var right = w - Px(MarginRight);
        var available = right - left - Px(ColumnGap);
        var rightWidth = Math.Max(Px(RightColumnWidth), (int)Math.Round(available * RightColumnRatio));
        var leftWidth = Math.Max(Px(200), available - rightWidth);
        var leftEnd = left + leftWidth;
        var rightStart = leftEnd + Px(ColumnGap);
        var listBottom = h - Px(ListBottomGap);
        var footerTop = h - Px(FooterBottomGap);

        // 상단 메뉴
        CenterY(_formatLabel, left - Px(3), HeaderTop, HeaderHeight);
        CenterY(_mipRadio, left + Px(110), HeaderTop, HeaderHeight);
        CenterY(_drmRadio, left + Px(239), HeaderTop, HeaderHeight);
        _startButton.SetBounds(right - Px(119), Px(HeaderTop), Px(119), Px(HeaderHeight));
        _infoButton.SetBounds(_startButton.Left - Px(13) - Px(18), Px(HeaderTop) + (Px(HeaderHeight) - Px(18)) / 2, Px(18), Px(18));

        // 대상 파일 / 폴더
        CenterY(_targetLabel, left - Px(3), SectionTop, ControlHeight);
        _targetFind.SetBounds(leftEnd - Px(FindWidth), Px(SectionTop), Px(FindWidth), Px(ControlHeight));
        // 왼쪽 열이 좁아지면 제목과 겹치지 않도록 콤보 폭을 줄인다
        var targetComboWidth = Math.Clamp(_targetFind.Left - Px(ComboGap) - (_targetLabel.Right + Px(8)), Px(90), Px(ComboWidth));
        CenterY(_targetCombo, _targetFind.Left - Px(ComboGap) - targetComboWidth, SectionTop, ControlHeight, targetComboWidth);
        _targetList.SetBounds(left, Px(ListTop), leftWidth, Math.Max(Px(60), listBottom - Px(ListTop)));

        // 저장 위치
        CenterY(_saveLabel, rightStart, SectionTop, ControlHeight);
        _saveFind.SetBounds(right - Px(FindWidth), Px(SectionTop), Px(FindWidth), Px(ControlHeight));
        CenterY(_saveCombo, _saveFind.Left - Px(ComboGap) - Px(ComboWidth), SectionTop, ControlHeight, Px(ComboWidth));
        _savePathPanel.SetBounds(rightStart, Px(PathTop), right - rightStart, Px(PathHeight));
        _savePathBox.SetBounds(Px(6), (_savePathPanel.Height - _savePathBox.Height) / 2, _savePathPanel.Width - Px(12), _savePathBox.Height);
        _sameFolderCheck.Location = new Point(rightStart + Px(1), Px(CheckCenterY) - _sameFolderCheck.Height / 2);

        // 변환 상태
        CenterY(_statusLabel, rightStart, StatusHeaderTop, StatusHeaderHeight);
        _logButton.SetBounds(right - Px(100), Px(StatusHeaderTop), Px(100), Px(StatusHeaderHeight));
        _statusList.SetBounds(rightStart, Px(StatusListTop), right - rightStart, Math.Max(Px(60), listBottom - Px(StatusListTop)));

        // 하단
        _resetButton.SetBounds(left, footerTop, Px(118), Px(ControlHeight));
        _closeButton.SetBounds(right - Px(80), footerTop, Px(80), Px(ControlHeight));

        LayoutColumns();
    }

    /// <summary>top/height(96 DPI 기준) 영역의 세로 가운데에 배치</summary>
    private void CenterY(Control control, int x, int top, int height, int? width = null)
    {
        if (width is { } wv) control.Width = wv;
        control.Location = new Point(x, Px(top) + (Px(height) - control.Height) / 2);
    }

    private void LayoutColumns()
    {
        // 크기/암호화 컬럼 고정, 창 크기 조절 시 파일/폴더명 컬럼 크기가 변경 (디자인 가이드(1)-2)
        // 경로는 가장 뒤 컬럼(V1.2): 내용 길이만큼 넓히고, 보이지 않는 부분은 가로 스크롤
        var t = _targetList.ClientSize.Width;
        int size = Px(84), enc = Px(89);
        var name = Math.Max(Px(150), t - size - enc - Px(82));
        var pathContent = Px(240);
        foreach (ListViewItem item in _targetList.Items)
            pathContent = Math.Max(pathContent, TextRenderer.MeasureText(item.SubItems[3].Text, _targetList.Font).Width + Px(16));
        _targetList.Columns[0].Width = name;
        _targetList.Columns[1].Width = size;
        _targetList.Columns[2].Width = enc;
        _targetList.Columns[3].Width = Math.Max(pathContent, t - name - size - enc);

        // 진행 상태 컬럼 고정, 파일명 컬럼이 나머지
        var s = _statusList.ClientSize.Width;
        var status = Px(134);
        _statusList.Columns[1].Width = status;
        _statusList.Columns[0].Width = Math.Max(Px(100), s - status - Px(28));
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
        LayoutColumns();
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
