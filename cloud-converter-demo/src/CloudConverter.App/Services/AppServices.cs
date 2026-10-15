using CloudConverter.Core;

namespace CloudConverter.App.Services;

/// <summary>설정 파일, 저장 경로, 변환 엔진 등 앱 전역 서비스</summary>
internal sealed class AppServices
{
    public static string ExecutablePath => Environment.ProcessPath ?? Application.ExecutablePath;
    public static string ExecutableDirectory => Path.GetDirectoryName(ExecutablePath)!;
    public static string ConfigDirectory => Path.Combine(ExecutableDirectory, "config");

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CloudConverterDemo");

    public static string DocumentsDemoDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CloudConverterDemo");

    public DemoSettings Settings { get; }
    public ExtensionRules Rules { get; }
    public ConversionPolicy Policy { get; }
    public LogClassification Classification { get; private set; }
    public ProtectionStateStore States { get; }
    public ConversionEngine Engine { get; }
    public LogStore Logs { get; }
    public CloudLocations Cloud { get; }
    public string SampleFolder { get; }
    public List<string> ConfigErrors { get; } = new();

    public AppServices()
    {
        Settings = Load("demo-settings.json", () => new DemoSettings());
        Rules = Load("extension-rules.json", ExtensionRules.CreateDefault);
        Policy = Load("conversion-policy.json", ConversionPolicy.CreateDefault);
        Classification = Load("log-classification.json", LogClassification.CreateDefault);

        // 변환 기록은 실행 중에만 유지: 실행할 때마다 모든 파일이 암호화되지 않은 상태로 시작
        States = new ProtectionStateStore();
        try { File.Delete(Path.Combine(DataDirectory, "protection-state.json")); } // 이전 버전이 남긴 기록
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        Engine = new ConversionEngine(Rules, Policy, States);
        Logs = new LogStore(Path.Combine(DataDirectory, "Logs"));

        var cloudRoot = string.IsNullOrWhiteSpace(Settings.CloudRoot)
            ? Path.Combine(DocumentsDemoDirectory, "Cloud")
            : Environment.ExpandEnvironmentVariables(Settings.CloudRoot);
        Cloud = new CloudLocations(cloudRoot, Settings);
        SampleFolder = Path.Combine(DocumentsDemoDirectory, "샘플 문서");

        if (Settings.CreateSampleFiles)
        {
            try
            {
                SampleFiles.EnsureDemoContent(cloudRoot, Settings, SampleFolder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ConfigErrors.Add("샘플 파일 생성 실패: " + ex.Message);
            }
        }
        try
        {
            foreach (var site in Settings.SharePointSites)
                Directory.CreateDirectory(Cloud.SharePointSitePath(site.Name));
            Directory.CreateDirectory(Cloud.OneDriveRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ConfigErrors.Add("가상 클라우드 폴더 생성 실패: " + ex.Message);
        }
    }

    /// <summary>config\log-classification.json 을 다시 읽는다 (로그 뷰어를 열 때마다 호출: 파일만 바꿔 넣으면 바로 반영)</summary>
    public LogClassification ReloadClassification()
    {
        Classification = Load("log-classification.json", LogClassification.CreateDefault);
        return Classification;
    }

    private T Load<T>(string fileName, Func<T> fallback) =>
        JsonConfig.Load(Path.Combine(ConfigDirectory, fileName), fallback, error => ConfigErrors.Add(error));
}

/// <summary>
/// 오프라인 시연용 가상 OneDrive / SharePoint.
/// 실제로는 로컬 폴더(문서\CloudConverterDemo\Cloud)이며, 화면에는 클라우드 경로로 표시한다.
/// </summary>
internal sealed class CloudLocations
{
    private readonly DemoSettings _settings;

    public string Root { get; }
    public string OneDriveRoot => Path.Combine(Root, "OneDrive");
    public string SharePointRoot => Path.Combine(Root, "SharePoint");

    public CloudLocations(string root, DemoSettings settings)
    {
        Root = root;
        _settings = settings;
    }

    public string SharePointSitePath(string site) => Path.Combine(SharePointRoot, site);

    public string RootFor(LocationKind kind) => kind switch
    {
        LocationKind.OneDrive => OneDriveRoot,
        LocationKind.SharePoint => SharePointRoot,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public IReadOnlyList<SharePointSite> Sites => _settings.SharePointSites;

    /// <summary>실제 로컬 경로 → 화면 표시 경로 (OneDrive\문서\파일.docx, SharePoint\영업팀\공유 문서\...)</summary>
    public string ToDisplay(string fullPath)
    {
        if (IsUnder(fullPath, OneDriveRoot))
            return Combine("OneDrive", Path.GetRelativePath(OneDriveRoot, fullPath));
        if (IsUnder(fullPath, SharePointRoot))
            return Combine("SharePoint", Path.GetRelativePath(SharePointRoot, fullPath));
        return fullPath;
    }

    public LocationKind? KindOf(string fullPath)
    {
        if (IsUnder(fullPath, OneDriveRoot)) return LocationKind.OneDrive;
        if (IsUnder(fullPath, SharePointRoot)) return LocationKind.SharePoint;
        return null;
    }

    private static string Combine(string head, string relative) => relative == "." ? head : head + "\\" + relative;

    private static bool IsUnder(string path, string root)
    {
        var full = Path.GetFullPath(path).TrimEnd('\\');
        var r = Path.GetFullPath(root).TrimEnd('\\');
        return full.Equals(r, StringComparison.OrdinalIgnoreCase)
               || full.StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase);
    }
}
