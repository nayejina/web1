namespace CloudConverter.Core;

/// <summary>
/// 저장 위치 파일명 중복 시 정책 (기획서 12p):
///  - 로컬 → 로컬 : 저장 위치의 기존 파일은 삭제(덮어쓰기)하고 변환된 파일을 같은 이름으로 저장.
///                  같은 작업 안에서 이름이 겹치면 첫 번째는 같은 이름, 이후는 파일명(1), (2)...
///  - 로컬 → SPO/OneDrive, SPO/OneDrive → 로컬 : 기존 파일을 유지하고 파일명(1).확장자 형식으로 저장
/// </summary>
public sealed class DestinationPlanner
{
    private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);
    private readonly ExtensionRules _rules;
    private readonly ConversionFormat _format;
    private readonly LocationKind _sourceKind;
    private readonly LocationKind _targetKind;
    private readonly string? _saveRoot;
    private readonly Func<string, bool> _exists;

    /// <param name="saveRoot">저장 폴더. null 이면 "대상 파일과 동일 폴더에 저장"</param>
    public DestinationPlanner(ExtensionRules rules, ConversionFormat format, LocationKind sourceKind,
        LocationKind targetKind, string? saveRoot, Func<string, bool>? exists = null)
    {
        _rules = rules;
        _format = format;
        _sourceKind = sourceKind;
        _targetKind = targetKind;
        _saveRoot = saveRoot;
        _exists = exists ?? File.Exists;
    }

    public bool OverwritesExisting => _sourceKind == LocationKind.MyDrive && _targetKind == LocationKind.MyDrive;

    /// <param name="sourcePath">원본 파일 전체 경로</param>
    /// <param name="relativeDirectory">선택한 폴더 안의 파일이면 "선택폴더\하위폴더" 형태의 상대 경로, 아니면 빈 문자열</param>
    public string Plan(string sourcePath, string relativeDirectory = "")
    {
        var outputName = _rules.GetOutputFileName(Path.GetFileName(sourcePath), _format);
        var directory = _saveRoot is null
            ? Path.GetDirectoryName(sourcePath)!
            : Path.Combine(_saveRoot, relativeDirectory);
        var desired = Path.Combine(directory, outputName);

        var candidate = desired;
        var counter = 0;
        while (_used.Contains(candidate) || (!OverwritesExisting && _exists(candidate) && !IsSame(candidate, sourcePath)))
        {
            counter++;
            candidate = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(outputName)}({counter}){Path.GetExtension(outputName)}");
        }
        _used.Add(candidate);
        return candidate;
    }

    private static bool IsSame(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
