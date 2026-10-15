using CloudConverter.App.Services;
using CloudConverter.App.UI;
using CloudConverter.Core;

namespace CloudConverter.App.Forms;

/// <summary>변환 실행 / 변환 상태 표시 (기획서 17p, 18p)</summary>
internal sealed partial class MainForm
{
    private sealed record WorkFile(string Path, string RelativeDirectory);

    private sealed class RunSummary
    {
        public int Success, Fail, Skip;
    }

    private async void StartConversion()
    {
        if (_running || _format is not { } format) return;
        var sameFolder = _sameFolderCheck.Checked;
        if (!sameFolder && _savePath is null) return;

        // 확인 메시지: 파일은 암호화 <가능> 파일만, 폴더는 폴더 개수만 카운팅
        var fileCount = ConvertibleFileCount;
        var folderCount = FolderCount;
        if (!MessageDialog.Confirm(this,
                $"변환 대상 파일: {fileCount}개\n변환 대상 폴더: {folderCount}개\n\n" +
                "실행된 작업은 취소가 불가능합니다. 계속하시겠습니까?\n(변환 불가능한 파일은 건너뛰고 진행합니다.)"))
            return;

        // MIP 버전: 로그인 / 적용할 레이블 선택 등 변환 전 준비
        if (_services.Engine is IPreparableEngine preparable && !await preparable.PrepareAsync(format, this))
            return;

        var targets = TargetItems.ToList();
        var folderFiles = new Dictionary<TargetItem, List<WorkFile>>();
        UseWaitCursor = true;
        try
        {
            foreach (var folder in targets.Where(t => t.IsFolder))
                folderFiles[folder] = await Task.Run(() => EnumerateFolder(folder.FullPath));
        }
        finally
        {
            UseWaitCursor = false;
        }

        // 정책 서버에서 설정한 파일 개수 / 용량 초과 (0 = 제한 없음)
        var policy = _services.Policy;
        var plannedFiles = targets.Where(t => !t.IsFolder && t.Eligibility.Convertible).Select(t => t.FullPath)
            .Concat(folderFiles.Values.SelectMany(f => f).Select(f => f.Path)
                .Where(p => _services.Engine.CheckEligibility(p).Convertible))
            .ToList();
        var totalBytes = plannedFiles.Sum(p => SafeLength(p));
        if ((policy.MaxFileCount > 0 && plannedFiles.Count > policy.MaxFileCount)
            || (policy.MaxTotalSizeMB > 0 && totalBytes > policy.MaxTotalSizeMB * 1024 * 1024))
        {
            MessageDialog.Info(this, "한 번에 변환 가능한 파일 개수/용량을 초과하였습니다.\n관리자에게 문의하세요.");
            return;
        }

        _running = true;
        _stopRequested = false;
        _statusList.Items.Clear();
        UpdateUiState();

        var jobId = Guid.NewGuid().ToString();
        var planner = new DestinationPlanner(_services.Rules, format, TargetKind, SaveKind, sameFolder ? null : _savePath);
        var summary = new RunSummary();
        var completed = new List<TargetItem>();

        // 실제 툴과 같이 변환할 파일을 먼저 모두 "준비 중"으로 올려 두고 순서대로 진행
        var rows = new Dictionary<string, StatusRow>(StringComparer.OrdinalIgnoreCase);
        _statusList.BeginUpdate();
        foreach (var target in targets)
        {
            rows[target.FullPath] = AddStatusRow(new StatusRow
            {
                Name = target.Name, IconKey = _shellIcons.KeyFor(target.FullPath, target.IsFolder), IsFolder = target.IsFolder,
                Style = StatusStyle.Pending, Text = "준비 중",
            }, ensureVisible: false);
            if (!target.IsFolder) continue;
            // 폴더 안의 변환 대상이 아닌 파일은 상태 목록에 올리지 않고 로그에만 남긴다
            foreach (var file in folderFiles[target].Where(f => _services.Engine.CheckEligibility(f.Path).Convertible))
            {
                rows[file.Path] = AddStatusRow(new StatusRow
                {
                    Name = Path.GetFileName(file.Path), IconKey = _shellIcons.KeyFor(file.Path, false),
                    Style = StatusStyle.Pending, Text = "준비 중",
                }, ensureVisible: false);
            }
        }
        _statusList.EndUpdate();

        try
        {
            foreach (var target in targets)
            {
                if (_stopRequested) break;
                if (!target.IsFolder)
                {
                    var code = await ProcessFile(target.FullPath, "", target.Eligibility, planner, format, jobId, summary, rows[target.FullPath]);
                    if (_services.Classification.GroupForCode(code).Style == "Success")
                        completed.Add(target);
                    continue;
                }

                // V1.3: 폴더 변환 시작/종료 알림 (선택한 폴더에 한해, 하위 폴더는 표시하지 않음)
                var folderRow = rows[target.FullPath];
                folderRow.Style = StatusStyle.Info;
                folderRow.Text = "변환 시작";
                RefreshRow(folderRow);
                Log(ResultCodes.FolderStarted, format, target.FullPath, "", jobId);
                var folderFailed = false;
                foreach (var file in folderFiles[target])
                {
                    if (_stopRequested) break;
                    var eligibility = _services.Engine.CheckEligibility(file.Path);
                    // 폴더 안의 변환 대상이 아닌 파일은 상태 목록에 올리지 않고 로그에만 남긴다
                    var code = await ProcessFile(file.Path, file.RelativeDirectory, eligibility, planner, format, jobId, summary,
                        rows.GetValueOrDefault(file.Path));
                    if (_services.Classification.GroupForCode(code).Style == "Fail") folderFailed = true;
                }
                var stoppedInFolder = _stopRequested;
                folderRow.Text = stoppedInFolder ? "변환 중지" : "변환 완료";
                RefreshRow(folderRow);
                Log(ResultCodes.FolderCompleted, format, target.FullPath, "", jobId);
                if (!folderFailed && !stoppedInFolder)
                    completed.Add(target);
            }
        }
        finally
        {
            var stopped = _stopRequested;
            foreach (var pending in rows.Values.Where(r => r.Style == StatusStyle.Pending))
                pending.Text = "중지됨";
            _statusList.Invalidate();
            _running = false;
            _stopRequested = false;

            // V1.0: 변환 완료된 항목은 대상 목록에서 삭제
            _targetList.BeginUpdate();
            foreach (var item in _targetList.Items.Cast<ListViewItem>().Where(i => completed.Contains((TargetItem)i.Tag!)).ToList())
                _targetList.Items.Remove(item);
            _targetList.EndUpdate();
            RefreshTargetCombo();
            UpdateUiState();
            _logViewer?.ReloadEntriesIfOpen();

            if (_closeAfterStop)
            {
                Close();
            }
            else
            {
                var title = stopped ? "작업이 중지되었습니다." : "변환이 완료되었습니다.";
                MessageDialog.Info(this, $"{title}\n\n성공 {summary.Success}개 / 실패 {summary.Fail}개 / 스킵 {summary.Skip}개");
            }
        }
    }

    private void RequestStop()
    {
        if (!_running || _stopRequested) return;
        if (MessageDialog.Confirm(this, "파일 작업을 중지하시겠습니까?\n중지하더라도 이미 변환된 파일은 변환된 상태로 유지됩니다.", "알림"))
            _stopRequested = true;
    }

    private static List<WorkFile> EnumerateFolder(string folder)
    {
        var name = Path.GetFileName(folder.TrimEnd('\\'));
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System };
        var result = new List<WorkFile>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", options).OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase))
            {
                if (file.EndsWith(".ccdemo.tmp", StringComparison.OrdinalIgnoreCase)) continue;
                var relative = Path.GetRelativePath(folder, Path.GetDirectoryName(file)!);
                result.Add(new WorkFile(file, relative == "." ? name : Path.Combine(name, relative)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return result;
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0; }
    }

    /// <summary>파일 1개 처리 + 진행 상태 애니메이션. 반환값: 결과 코드</summary>
    private async Task<int> ProcessFile(string path, string relativeDirectory, Eligibility eligibility, DestinationPlanner planner,
        ConversionFormat format, string jobId, RunSummary summary, StatusRow? row)
    {
        if (row is not null)
        {
            row.Style = StatusStyle.Running;
            row.Progress = 0;
            EnsureVisible(row);
            RefreshRow(row);
        }

        int code;
        var detail = "";
        string destination = "";
        if (!eligibility.Convertible)
        {
            code = eligibility.ReasonCode;
        }
        else
        {
            // 실제 판정/복사를 먼저 수행하고, 결과에 맞춰 진행 상태를 보여 준다
            var size = SafeLength(path);
            var duration = _services.Policy.Demo.DurationFor(size);
            destination = planner.Plan(path, relativeDirectory);
            var dest = destination;
            var work = Task.Run(() => _services.Engine.Convert(path, dest, format));
            await AnimateUntil(row, work, duration);
            var outcome = await work;
            code = outcome.Code;
            detail = outcome.Detail;
            destination = outcome.OutputPath ?? destination;
        }

        var group = _services.Classification.GroupForCode(code);
        var rule = _services.Classification.Rule(code);
        switch (group.Style)
        {
            case "Success": summary.Success++; break;
            case "Fail": summary.Fail++; break;
            default: summary.Skip++; break;
        }

        if (row is not null)
        {
            if (group.Style == "Success")
            {
                await AnimateTo(row, 100, Math.Max(150, _services.Policy.Demo.MinFileDurationMs / 3));
                row.Style = StatusStyle.Success;
                row.OutputPath = destination;
            }
            else if (group.Style == "Fail")
            {
                // 진행도 올라가다 실패 상황이 되면 전체가 회색으로 바뀌고 "실패" 표시
                await AnimateTo(row, Math.Max(row.Progress, 35 + Random.Shared.Next(30)), 300);
                row.Style = StatusStyle.Fail;
                row.Text = group.Display;
            }
            else
            {
                // V1.3: SKIP 은 성공보다 연한 색으로
                row.Style = StatusStyle.Skip;
                row.Text = "SKIP";
            }
            row.Reason = detail.Length == 0 ? rule.Reason : rule.Reason + "\n" + detail;
            RefreshRow(row);
        }

        Log(code, format, path, group.Style == "Success" ? destination : "", jobId, detail);
        return code;
    }

    /// <summary>변환 작업이 끝날 때까지 90% 까지 진행 표시</summary>
    private async Task AnimateUntil(StatusRow? row, Task work, int durationMs)
    {
        var started = Environment.TickCount64;
        while (true)
        {
            var elapsed = Environment.TickCount64 - started;
            if (row is not null)
            {
                var target = (int)Math.Min(90, elapsed * 90 / Math.Max(1, durationMs));
                if (target != row.Progress)
                {
                    row.Progress = target;
                    RefreshRow(row);
                }
            }
            if (work.IsCompleted && (elapsed >= durationMs || _stopRequested))
                return;
            await Task.Delay(40);
        }
    }

    private async Task AnimateTo(StatusRow row, int target, int durationMs)
    {
        var start = row.Progress;
        var started = Environment.TickCount64;
        while (row.Progress < target)
        {
            var elapsed = Environment.TickCount64 - started;
            row.Progress = (int)Math.Min(target, start + (target - start) * elapsed / Math.Max(1, durationMs));
            RefreshRow(row);
            if (row.Progress >= target) break;
            await Task.Delay(30);
        }
    }

    private StatusRow AddStatusRow(StatusRow row, bool ensureVisible = true)
    {
        var item = new ListViewItem(row.Name, row.IconKey) { Tag = row };
        item.SubItems.Add("");
        _statusList.Items.Add(item);
        if (ensureVisible) item.EnsureVisible();
        return row;
    }

    private void EnsureVisible(StatusRow row)
    {
        foreach (ListViewItem item in _statusList.Items)
        {
            if (ReferenceEquals(item.Tag, row))
            {
                item.EnsureVisible();
                return;
            }
        }
    }

    private void RefreshRow(StatusRow row)
    {
        foreach (ListViewItem item in _statusList.Items)
        {
            if (ReferenceEquals(item.Tag, row))
            {
                _statusList.RedrawItems(item.Index, item.Index, true);
                return;
            }
        }
    }

    private void Log(int code, ConversionFormat format, string source, string destination, string jobId, string detail = "")
    {
        try
        {
            _services.Logs.Append(new LogEntry
            {
                Time = DateTime.Now,
                Code = code,
                Format = format,
                FileName = Path.GetFileName(source.TrimEnd('\\')),
                SourcePath = DisplayOf(source),
                DestinationPath = destination.Length == 0 ? "" : DisplayOf(destination),
                JobId = jobId,
                Detail = detail,
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        _logViewer?.ReloadEntriesIfOpen();
    }

    // ───────────────────────────── 진행 상태 그리기 ─────────────────────────────

    private void DrawStatusSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item?.Tag is not StatusRow row) return;
        var g = e.Graphics;
        var bounds = e.Bounds;
        var selected = e.Item.Selected && _statusList.Focused;
        using (var back = new SolidBrush(selected ? Color.FromArgb(0xCC, 0xE4, 0xF7) : _statusList.BackColor))
            g.FillRectangle(back, bounds);

        if (e.ColumnIndex == 0)
        {
            var iconSize = _icons.ImageSize;
            var iconRect = new Rectangle(bounds.X + Px(4), bounds.Y + (bounds.Height - iconSize.Height) / 2, iconSize.Width, iconSize.Height);
            if (_icons.Images.ContainsKey(row.IconKey))
                g.DrawImage(_icons.Images[row.IconKey]!, iconRect);
            var textRect = new Rectangle(iconRect.Right + Px(4), bounds.Y, bounds.Right - iconRect.Right - Px(4), bounds.Height);
            TextRenderer.DrawText(g, row.Name, _statusList.Font, textRect, _statusList.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            return;
        }

        if (row.Style == StatusStyle.Info)
        {
            TextRenderer.DrawText(g, row.Text, _statusList.Font, bounds, Theme.Accent,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
            return;
        }

        // 실제 툴: 셀을 거의 채우는 막대, 진행 #1E90FF + 흰 글자, 준비 중은 흰 칸 + 회색 테두리
        var bar = Rectangle.Inflate(bounds, -Px(2), -Px(2));
        if (bar.Width <= 4 || bar.Height <= 4) return;
        using (var track = new SolidBrush(Theme.ProgressTrack)) g.FillRectangle(track, bar);

        string text;
        Color fill;
        var percent = row.Progress;
        switch (row.Style)
        {
            case StatusStyle.Pending:
                fill = Theme.ProgressTrack; percent = 0; text = row.Text; break;
            case StatusStyle.Fail:
                fill = Theme.ProgressFail; percent = 100; text = row.Text; break;
            case StatusStyle.Skip:
                fill = Theme.ProgressSkip; percent = 100; text = row.Text; break;
            case StatusStyle.Success:
                fill = Theme.ProgressBar; percent = 100; text = "100%"; break;
            default:
                fill = Theme.ProgressBar; text = $"{row.Progress}%"; break;
        }
        var filled = new Rectangle(bar.X, bar.Y, bar.Width * Math.Clamp(percent, 0, 100) / 100, bar.Height);
        using (var brush = new SolidBrush(fill)) g.FillRectangle(brush, filled);
        if (percent < 100)
        {
            using var pen = new Pen(Theme.PendingBorder);
            g.DrawRectangle(pen, bar.X, bar.Y, bar.Width - 1, bar.Height - 1);
        }

        const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine;
        if (row.Style is StatusStyle.Fail or StatusStyle.Skip or StatusStyle.Pending)
        {
            TextRenderer.DrawText(g, text, _statusList.Font, bar, Color.Black, flags);
            return;
        }
        // 글자가 파란 부분에 완전히 덮이면 흰색, 아니면 검정
        var textWidth = TextRenderer.MeasureText(g, text, _statusList.Font, bar.Size, flags).Width;
        var textRight = bar.X + (bar.Width + textWidth) / 2;
        TextRenderer.DrawText(g, text, _statusList.Font, bar, filled.Right >= textRight ? Color.White : Color.Black, flags);
    }
}
