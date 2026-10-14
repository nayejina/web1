namespace CloudConverter.Core;

/// <summary>브랜딩 / 프로그램 정보 / 가상 클라우드 설정 (config/demo-settings.json)</summary>
public sealed class DemoSettings
{
    /// <summary>프로그램 정보 창 하단에 항상 표시되는 안내 문구 (설정으로 끌 수 없음)</summary>
    public const string Disclaimer = "본 애플리케이션은 시연용으로, 실제 MIP 변환 기능을 제공하지 않습니다.";

    public string WindowTitle { get; set; } = "CloudConverter";
    public string ProductName { get; set; } = "CloudConverter";
    public string Account { get; set; } = "demo.user@ableinfo.net";
    public string Version { get; set; } = "1.4.0 (Demo)";
    public string Copyright { get; set; } = "Copyright (C) 2025 Ableinfo Corp. - All rights reserved.";

    /// <summary>
    /// 오프라인 시연용 가상 클라우드 루트 폴더. 비워 두면 문서\CloudConverterDemo\Cloud 를 사용.
    /// 이 아래 OneDrive, SharePoint\사이트명 폴더가 OneDrive / SharePoint 로 보인다.
    /// </summary>
    public string CloudRoot { get; set; } = "";

    public List<SharePointSite> SharePointSites { get; set; } = new()
    {
        new() { Name = "전사 공지", Description = "여기서 조직의 공지 사항 및 중요한 정보를 확인하세요." },
        new() { Name = "영업팀", Description = "영업팀 문서 공유 사이트" },
        new() { Name = "기술연구소", Description = "기술연구소 프로젝트 문서" },
    };

    /// <summary>처음 실행 시 가상 클라우드와 샘플 폴더에 시연용 파일을 만든다</summary>
    public bool CreateSampleFiles { get; set; } = true;

    /// <summary>실행 시 탐색기 우클릭 메뉴를 자동 등록 (HKCU, 관리자 권한 불필요)</summary>
    public bool AutoRegisterContextMenu { get; set; } = true;
}

public sealed class SharePointSite
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}
