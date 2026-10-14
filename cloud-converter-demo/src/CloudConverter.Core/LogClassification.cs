namespace CloudConverter.Core;

/// <summary>변환 엔진이 내보내는 결과 코드. 화면/로그에 보이는 결과·분류 문구는 log-classification.json 이 결정한다.</summary>
public static class ResultCodes
{
    public const int MipSuccess = 1000;
    public const int DrmSuccess = 1001;

    public const int AlreadyMip = 2001;
    public const int AlreadyDrm = 2002;
    public const int AlreadyEncrypted = 2003;
    public const int FileAccessError = 2004;
    public const int DemoForcedFail = 2005;
    public const int SourceNotFound = 2006;

    public const int UnsupportedExtension = 3001;
    public const int EmptyFile = 3002;
    public const int PolicySkip = 3003;
    public const int ExcludedDirectory = 3004;
    public const int UserStopped = 3005;

    public const int FolderStarted = 9001;
    public const int FolderCompleted = 9002;
}

/// <summary>결과 그룹 (성공/실패/스킵 ...). 화면 진행 상태 막대 색상도 여기서 정한다.</summary>
public sealed class ResultGroup
{
    public string Id { get; set; } = "";
    public string Display { get; set; } = "";
    /// <summary>진행 상태 막대 표시 방식: Success | Fail | Skip | Info</summary>
    public string Style { get; set; } = "Info";
}

public sealed class ResultCodeRule
{
    public int Code { get; set; }
    /// <summary>ResultGroup.Id</summary>
    public string Group { get; set; } = "";
    /// <summary>로그의 "분류" 항목</summary>
    public string Category { get; set; } = "";
    /// <summary>툴팁 / 진행 상태에 보여 줄 짧은 사유</summary>
    public string Reason { get; set; } = "";
}

/// <summary>
/// 로그 결과 분류 (config/log-classification.json).
/// 기획서 20p 문의사항(성공/실패/스킵 분류)은 확정 전이므로, 이 파일을 교체/수정하면
/// 코드 변경 없이 결과 그룹과 분류 문구를 바꿀 수 있다.
/// </summary>
public sealed class LogClassification
{
    public List<ResultGroup> Groups { get; set; } = new();
    public List<ResultCodeRule> Codes { get; set; } = new();

    public static LogClassification CreateDefault() => new()
    {
        Groups = new()
        {
            new() { Id = "success", Display = "성공", Style = "Success" },
            new() { Id = "fail", Display = "실패", Style = "Fail" },
            new() { Id = "skip", Display = "스킵", Style = "Skip" },
            new() { Id = "info", Display = "알림", Style = "Info" },
        },
        Codes = new()
        {
            new() { Code = ResultCodes.MipSuccess, Group = "success", Category = "MIP 변환 성공", Reason = "MIP 레이블이 적용되었습니다." },
            new() { Code = ResultCodes.DrmSuccess, Group = "success", Category = "DRM 변환 성공", Reason = "DRM 문서로 변환되었습니다." },
            new() { Code = ResultCodes.AlreadyMip, Group = "fail", Category = "이미 암호화된 파일", Reason = "이미 암호화된 파일" },
            new() { Code = ResultCodes.AlreadyDrm, Group = "fail", Category = "이미 DRM 문서인 파일", Reason = "이미 DRM 문서인 파일" },
            new() { Code = ResultCodes.AlreadyEncrypted, Group = "fail", Category = "이미 암호화된 파일", Reason = "이미 암호화된 파일" },
            new() { Code = ResultCodes.FileAccessError, Group = "fail", Category = "파일 접근 실패", Reason = "파일에 접근할 수 없습니다 (사용 중이거나 권한 없음)" },
            new() { Code = ResultCodes.DemoForcedFail, Group = "fail", Category = "레이블 변환 권한 없음", Reason = "레이블 변환 권한 없음" },
            new() { Code = ResultCodes.SourceNotFound, Group = "fail", Category = "원본 파일 없음", Reason = "원본 파일을 찾을 수 없습니다" },
            new() { Code = ResultCodes.UnsupportedExtension, Group = "skip", Category = "대상 확장자가 아닌 경우", Reason = "지원하지 않는 확장자" },
            new() { Code = ResultCodes.EmptyFile, Group = "skip", Category = "파일 사이즈가 0", Reason = "파일 크기가 0 입니다" },
            new() { Code = ResultCodes.PolicySkip, Group = "skip", Category = "정책에 의한 스킵", Reason = "변환 규칙에 해당하는 레이블이 아닙니다." },
            new() { Code = ResultCodes.ExcludedDirectory, Group = "skip", Category = "제외 디렉토리", Reason = "제외 디렉토리에 있는 파일" },
            new() { Code = ResultCodes.UserStopped, Group = "skip", Category = "사용자 작업 중지", Reason = "사용자가 작업을 중지했습니다" },
            new() { Code = ResultCodes.FolderStarted, Group = "info", Category = "폴더 변환 시작", Reason = "폴더 변환 시작" },
            new() { Code = ResultCodes.FolderCompleted, Group = "info", Category = "폴더 변환 완료", Reason = "폴더 변환 완료" },
        },
    };

    public ResultCodeRule Rule(int code) =>
        Codes.FirstOrDefault(c => c.Code == code)
        ?? CreateDefault().Codes.FirstOrDefault(c => c.Code == code)
        ?? new ResultCodeRule { Code = code, Group = "info", Category = $"코드 {code}", Reason = $"코드 {code}" };

    public ResultGroup Group(string id) =>
        Groups.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? new ResultGroup { Id = id, Display = id, Style = "Info" };

    public ResultGroup GroupForCode(int code) => Group(Rule(code).Group);
}
