using System.Globalization;
using CloudConverter.App.Services;
using CloudConverter.App.UI;
using CloudConverter.Core;

namespace CloudConverter.App.Forms;

/// <summary>로그 뷰어 (기획서 20p, 21p): 조회 기간, 검색어, 결과 필터, 상세 보기, CSV 내보내기</summary>
internal sealed class LogViewerForm : ThemedForm
{
    private sealed record GroupOption(string? Id, string Display)
    {
        public override string ToString() => Display;
    }

    private readonly AppServices _services;
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    private readonly ComboBox _groupFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly FlatButton _exportButton = new("내보내기(CSV)") { Width = 110 };
    private readonly TextBox _search = new() { Width = 180, PlaceholderText = "파일 이름 / 경로 / 분류" };
    private readonly ListView _list = new()
    {
        View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = true,
        Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, VirtualMode = true,
    };
    private readonly TextBox _detail = new()
    {
        Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, BackColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle, ScrollBars = ScrollBars.Vertical,
    };
    private readonly System.Windows.Forms.Timer _reloadTimer = new() { Interval = 400 };
    private LogClassification _classification;
    private List<LogEntry> _entries = new();
    private List<LogEntry> _view = new();

    public LogViewerForm(AppServices services)
    {
        _services = services;
        _classification = services.ReloadClassification();
        Text = $"{services.Settings.ProductName} Log Viewer";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(980, 620);
        MinimumSize = new Size(720, 420);
        BackColor = Theme.WindowBack;

        _from.Value = DateTime.Today;
        _to.Value = DateTime.Today;

        _list.Columns.Add("결과", 70);
        _list.Columns.Add("시간", 165);
        _list.Columns.Add("파일 이름", 240);
        _list.Columns.Add("파일 경로", 420);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 0) };
        toolbar.Controls.Add(MakeLabel("조회 기간:"));
        toolbar.Controls.Add(_from);
        toolbar.Controls.Add(MakeLabel("~"));
        toolbar.Controls.Add(_to);
        toolbar.Controls.Add(MakeLabel("   결과:"));
        toolbar.Controls.Add(_groupFilter);
        _exportButton.Margin = new Padding(12, 0, 0, 0);
        toolbar.Controls.Add(_exportButton);

        var searchPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 0) };
        searchPanel.Controls.Add(_search);
        searchPanel.Controls.Add(MakeLabel("검색어:"));

        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        top.Controls.Add(toolbar, 0, 0);
        top.Controls.Add(searchPanel, 1, 0);

        var detailLabel = new Label { Text = "상세 보기", AutoSize = true, Margin = new Padding(0, 8, 0, 4) };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        root.Controls.Add(top, 0, 0);
        root.Controls.Add(_list, 0, 1);
        root.Controls.Add(detailLabel, 0, 2);
        root.Controls.Add(_detail, 0, 3);
        Controls.Add(root);

        FillGroupFilter();

        _from.ValueChanged += (_, _) => ReloadEntries();
        _to.ValueChanged += (_, _) => ReloadEntries();
        _groupFilter.SelectedIndexChanged += (_, _) => ApplyFilter();
        _search.TextChanged += (_, _) => ApplyFilter();
        _exportButton.Click += (_, _) => Export();
        _list.RetrieveVirtualItem += (_, e) => e.Item = MakeItem(_view[e.ItemIndex]);
        _list.SelectedIndexChanged += (_, _) => ShowDetail();
        _list.VirtualItemsSelectionRangeChanged += (_, _) => ShowDetail();
        _list.Resize += (_, _) => LayoutColumns();
        _reloadTimer.Tick += (_, _) => { _reloadTimer.Stop(); ReloadEntries(); };

        ReloadEntries();
    }

    private static Label MakeLabel(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(4, 6, 4, 0) };

    private void FillGroupFilter()
    {
        _groupFilter.Items.Clear();
        _groupFilter.Items.Add(new GroupOption(null, "전체"));
        foreach (var group in _classification.Groups)
            _groupFilter.Items.Add(new GroupOption(group.Id, group.Display));
        _groupFilter.SelectedIndex = 0;
    }

    private void LayoutColumns()
    {
        var w = _list.ClientSize.Width;
        _list.Columns[0].Width = Px(70);
        _list.Columns[1].Width = Px(165);
        var rest = Math.Max(Px(300), w - _list.Columns[0].Width - _list.Columns[1].Width);
        _list.Columns[2].Width = rest * 2 / 5;
        _list.Columns[3].Width = rest - _list.Columns[2].Width;
    }

    /// <summary>변환 중 로그가 추가되면 잠시 뒤 자동 새로고침</summary>
    public void ReloadEntriesIfOpen()
    {
        if (IsDisposed) return;
        _reloadTimer.Stop();
        _reloadTimer.Start();
    }

    public void ReloadEntries()
    {
        if (IsDisposed) return;
        var from = _from.Value.Date;
        var to = _to.Value.Date;
        if (to < from) (from, to) = (to, from);
        // 최신 로그가 위로 (같은 초 안에서도 나중에 기록된 것이 위)
        _entries = _services.Logs.Read(from, to).Select((entry, index) => (entry, index))
            .OrderByDescending(x => x.entry.Time).ThenByDescending(x => x.index).Select(x => x.entry).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var groupId = (_groupFilter.SelectedItem as GroupOption)?.Id;
        var keyword = _search.Text.Trim();
        _view = _entries.Where(e =>
        {
            var rule = _classification.Rule(e.Code);
            if (groupId is not null && !string.Equals(rule.Group, groupId, StringComparison.OrdinalIgnoreCase))
                return false;
            if (keyword.Length == 0) return true;
            return e.FileName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                   || e.SourcePath.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                   || rule.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }).ToList();
        _list.SelectedIndices.Clear();
        _list.VirtualListSize = _view.Count;
        _list.Invalidate();
        ShowDetail();
    }

    private ListViewItem MakeItem(LogEntry entry)
    {
        var group = _classification.GroupForCode(entry.Code);
        var item = new ListViewItem(group.Display);
        item.SubItems.Add(entry.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        item.SubItems.Add(entry.FileName);
        item.SubItems.Add(entry.SourcePath);
        item.ForeColor = group.Style switch
        {
            "Fail" => Theme.Warning,
            "Skip" => Color.DimGray,
            "Info" => Theme.Accent,
            _ => Color.Black,
        };
        return item;
    }

    /// <summary>1개 선택 시 상세 보기, 다중 선택 시 빈 칸</summary>
    private void ShowDetail()
    {
        if (_list.SelectedIndices.Count != 1)
        {
            _detail.Text = "";
            return;
        }
        var e = _view[_list.SelectedIndices[0]];
        var rule = _classification.Rule(e.Code);
        var lines = new List<string>
        {
            $"결과: {_classification.Group(rule.Group).Display}",
            $"시간: {e.Time:yyyy-MM-dd tt h:mm:ss}",
            $"분류: {rule.Category}",
            $"분류 코드: {e.Code}",
            $"변환 형식: {e.Format.DisplayName()}",
            $"파일 경로: {e.SourcePath}",
        };
        if (e.DestinationPath.Length > 0) lines.Add($"저장 경로: {e.DestinationPath}");
        lines.Add($"부가 정보: '{e.JobId}'");
        _detail.Text = string.Join(Environment.NewLine, lines);
    }

    private void Export()
    {
        var selected = _list.SelectedIndices.Cast<int>().Select(i => _view[i]).ToList();
        var rows = selected.Count > 0 ? selected : _view;
        using var dialog = new SaveFileDialog
        {
            Title = "로그 내보내기",
            Filter = "CSV 파일|*.csv",
            FileName = $"{_services.Settings.ProductName}_Log_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            LogStore.Export(dialog.FileName, rows.OrderBy(r => r.Time), _classification);
            MessageDialog.Info(this, "로그 내보내기가 완료되었습니다.", "저장");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageDialog.Info(this, "로그를 저장하지 못했습니다.\n" + ex.Message, "저장");
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        LayoutColumns();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _reloadTimer.Dispose();
        base.OnFormClosed(e);
    }
}
