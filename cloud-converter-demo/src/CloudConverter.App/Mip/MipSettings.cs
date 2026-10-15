namespace CloudConverter.App.Mip;

/// <summary>MIP 버전 설정 (config\mip-settings.json)</summary>
internal sealed class MipSettings
{
    /// <summary>Entra ID 에 등록한 앱의 애플리케이션(클라이언트) ID. 필수</summary>
    public string ClientId { get; set; } = "";

    /// <summary>테넌트 ID 또는 도메인. 비우거나 "organizations" 면 로그인한 계정의 테넌트</summary>
    public string TenantId { get; set; } = "organizations";

    /// <summary>앱 등록의 "모바일 및 데스크톱 애플리케이션" 리디렉션 URI</summary>
    public string RedirectUri { get; set; } = "http://localhost";

    /// <summary>MIP SDK 에 전달하는 애플리케이션 이름 (감사 로그에 표시)</summary>
    public string ApplicationName { get; set; } = "CloudConverter";

    /// <summary>MIP 변환 시 적용할 레이블 ID. 비우면 변환을 시작할 때 레이블 선택 창을 띄운다</summary>
    public string LabelId { get; set; } = "";

    /// <summary>true 면 LabelId 와 관계없이 변환할 때마다 레이블을 고른다</summary>
    public bool AskLabelEveryTime { get; set; }

    /// <summary>레이블을 낮추거나 제거할 때 남기는 사유</summary>
    public string JustificationMessage { get; set; } = "CloudConverter 를 통한 레이블 변경";

    /// <summary>"DRM으로 변환" 시 MIP 보호 해제와 함께 레이블도 제거</summary>
    public bool RemoveLabelOnDrmConversion { get; set; }

    /// <summary>MIP SDK 로그 수준: Trace / Info / Warning / Error</summary>
    public string LogLevel { get; set; } = "Warning";
}
