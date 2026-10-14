namespace CloudConverter.Core;

/// <summary>대상 파일 위치 → 선택 가능한 저장 위치</summary>
public sealed class ConversionRoute
{
    public LocationKind Source { get; set; }
    public List<LocationKind> Targets { get; set; } = new();
}

/// <summary>
/// 고객사 정책 (config/conversion-policy.json).
/// 기획서 "기본 변환 목록" 및 관리 툴에서 설정하는 값을 시연용으로 파일에 둔다.
/// </summary>
public sealed class ConversionPolicy
{
    public List<ConversionRoute> MipRoutes { get; set; } = new();
    public List<ConversionRoute> DrmRoutes { get; set; } = new();

    /// <summary>한 번에 변환 가능한 파일 개수. 0 = 제한 없음</summary>
    public int MaxFileCount { get; set; }

    /// <summary>한 번에 변환 가능한 총 용량(MB). 0 = 제한 없음</summary>
    public long MaxTotalSizeMB { get; set; }

    /// <summary>제외 디렉토리 (이 경로 아래 파일은 "불가")</summary>
    public List<string> ExcludedDirectories { get; set; } = new();

    /// <summary>제외 디렉토리 키워드 (경로의 폴더 이름에 포함되면 "불가")</summary>
    public List<string> ExcludedDirectoryKeywords { get; set; } = new();

    /// <summary>시연 시나리오용 강제 결과 규칙</summary>
    public DemoRules Demo { get; set; } = new();

    public static ConversionPolicy CreateDefault() => new()
    {
        MipRoutes = new()
        {
            new() { Source = LocationKind.MyDrive, Targets = new() { LocationKind.MyDrive, LocationKind.OneDrive, LocationKind.SharePoint } },
            new() { Source = LocationKind.OneDrive, Targets = new() { LocationKind.MyDrive } },
            new() { Source = LocationKind.SharePoint, Targets = new() { LocationKind.MyDrive } },
        },
        DrmRoutes = new()
        {
            new() { Source = LocationKind.MyDrive, Targets = new() { LocationKind.MyDrive } },
            new() { Source = LocationKind.OneDrive, Targets = new() { LocationKind.MyDrive } },
            new() { Source = LocationKind.SharePoint, Targets = new() { LocationKind.MyDrive } },
        },
    };

    public List<ConversionRoute> RoutesFor(ConversionFormat format) =>
        format == ConversionFormat.Mip ? MipRoutes : DrmRoutes;

    public IReadOnlyList<LocationKind> SourcesFor(ConversionFormat format) =>
        RoutesFor(format).Select(r => r.Source).Distinct().ToList();

    public IReadOnlyList<LocationKind> TargetsFor(ConversionFormat format, LocationKind source) =>
        RoutesFor(format).Where(r => r.Source == source).SelectMany(r => r.Targets).Distinct().ToList();

    /// <summary>저장 위치를 먼저 고른 경우, 그 저장 위치와 매칭되는 대상 위치</summary>
    public IReadOnlyList<LocationKind> SourcesForTarget(ConversionFormat format, LocationKind target) =>
        RoutesFor(format).Where(r => r.Targets.Contains(target)).Select(r => r.Source).Distinct().ToList();

    public bool IsAllowed(ConversionFormat format, LocationKind source, LocationKind target) =>
        TargetsFor(format, source).Contains(target);

    public bool IsExcludedPath(string fullPath)
    {
        foreach (var dir in ExcludedDirectories)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var normalized = dir.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        var directory = Path.GetDirectoryName(fullPath) ?? "";
        var parts = directory.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        return ExcludedDirectoryKeywords.Any(k => !string.IsNullOrWhiteSpace(k)
            && parts.Any(p => p.Contains(k, StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed class DemoRules
{
    /// <summary>파일 이름에 포함되면 변환 "실패" 처리 (시연용)</summary>
    public List<string> ForceFailKeywords { get; set; } = new() { "_fail", "[실패]" };

    /// <summary>파일 이름에 포함되면 정책에 의한 "스킵" 처리 (시연용)</summary>
    public List<string> ForceSkipKeywords { get; set; } = new() { "_skip", "[스킵]" };

    /// <summary>진행 표시용 가상 변환 속도 (MB/s)</summary>
    public double SimulatedSpeedMBps { get; set; } = 40;

    /// <summary>파일 1개당 최소/최대 진행 시간 (ms)</summary>
    public int MinFileDurationMs { get; set; } = 700;
    public int MaxFileDurationMs { get; set; } = 4000;

    public int DurationFor(long sizeBytes)
    {
        var speed = SimulatedSpeedMBps <= 0 ? 40 : SimulatedSpeedMBps;
        var ms = (int)(sizeBytes / (speed * 1024 * 1024) * 1000);
        return Math.Clamp(ms, MinFileDurationMs, Math.Max(MinFileDurationMs, MaxFileDurationMs));
    }
}
