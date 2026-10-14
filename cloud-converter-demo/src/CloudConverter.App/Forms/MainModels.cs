using CloudConverter.Core;

namespace CloudConverter.App.Forms;

/// <summary>대상 파일/폴더 목록의 한 줄</summary>
internal sealed class TargetItem
{
    public required string FullPath { get; init; }
    public required bool IsFolder { get; init; }
    public required long Size { get; init; }
    public required Eligibility Eligibility { get; init; }
    public required LocationKind Location { get; init; }
    public required string DisplayPath { get; init; }
    public string Name => Path.GetFileName(FullPath.TrimEnd('\\'));
}

internal enum StatusStyle
{
    Running,
    Success,
    Fail,
    Skip,
    Info,
}

/// <summary>변환 상태 목록의 한 줄</summary>
internal sealed class StatusRow
{
    public required string Name { get; init; }
    public required string IconKey { get; init; }
    public bool IsFolder { get; init; }
    public int Progress { get; set; }
    public StatusStyle Style { get; set; } = StatusStyle.Running;
    /// <summary>Info / Fail / Skip 일 때 표시할 문구</summary>
    public string Text { get; set; } = "";
    /// <summary>툴팁 사유 (예: 이미 암호화된 파일)</summary>
    public string Reason { get; set; } = "";
    public string? OutputPath { get; set; }
}

internal static class SizeFormat
{
    public static string Format(long bytes)
    {
        const double kb = 1024, mb = kb * 1024, gb = mb * 1024;
        if (bytes < mb) return $"{Math.Ceiling(bytes / kb):0}KB";
        if (bytes < gb)
        {
            var v = bytes / mb;
            return v < 10 ? $"{v:0.#}MB" : $"{v:0}MB";
        }
        return $"{bytes / gb:0.#}GB";
    }
}
