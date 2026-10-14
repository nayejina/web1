namespace CloudConverter.App.UI;

/// <summary>
/// 화면 색상. 실제 Converter 툴 UI 기준: Windows 기본 제목 표시줄·폰트, 상단 메뉴는 밝은 배경 + 구분선,
/// &lt;변환 시작&gt; 버튼만 파란색. 나머지는 디자인 가이드(2) 색상.
/// </summary>
internal static class Theme
{
    // 상단 메뉴 (변환할 형식)
    public static readonly Color HeaderSeparator = ColorTranslator.FromHtml("#DADADA");
    public static readonly Color InfoIcon = ColorTranslator.FromHtml("#7A8794");

    // <변환 시작> 버튼
    public static readonly Color PrimaryBack = ColorTranslator.FromHtml("#3A6FA5");
    public static readonly Color PrimaryBackHover = ColorTranslator.FromHtml("#2F5D8C");
    public static readonly Color PrimaryText = Color.White;
    public static readonly Color PrimaryBackDisabled = ColorTranslator.FromHtml("#8294AA");
    public static readonly Color PrimaryTextDisabled = ColorTranslator.FromHtml("#ADC6DA");

    // 일반 버튼
    public static readonly Color ButtonBack = ColorTranslator.FromHtml("#CCCCCD");
    public static readonly Color ButtonText = Color.Black;
    public static readonly Color ButtonTextDisabled = ColorTranslator.FromHtml("#B2B3B3");

    // 전체 창 (파일/상태 영역 제외)
    public static readonly Color WindowBack = ColorTranslator.FromHtml("#F6F6F6");
    public static readonly Color ListBack = Color.White;
    public static readonly Color Border = ColorTranslator.FromHtml("#C8C8C8");
    public static readonly Color LabelDisabled = ColorTranslator.FromHtml("#A0A0A0");

    // 진행 상태
    public static readonly Color ProgressBar = ColorTranslator.FromHtml("#1F8BF3");
    public static readonly Color ProgressTrack = Color.White;
    public static readonly Color ProgressBorder = ColorTranslator.FromHtml("#9AA7B4");
    public static readonly Color ProgressFail = ColorTranslator.FromHtml("#C9C9C9");
    public static readonly Color ProgressSkip = ColorTranslator.FromHtml("#B9D7F4");
    public static readonly Color Accent = ColorTranslator.FromHtml("#0078D4");
    public static readonly Color Warning = ColorTranslator.FromHtml("#C42B1C");

    /// <summary>Windows 기본 폰트 (Control.DefaultFont) 의 굵은 글꼴</summary>
    public static Font Bold() => new(Control.DefaultFont, FontStyle.Bold);

    public static Icon AppIcon
    {
        get
        {
            using var stream = typeof(Theme).Assembly.GetManifestResourceStream("app.ico");
            return stream is null ? SystemIcons.Application : new Icon(stream);
        }
    }
}

/// <summary>모든 창의 공통 기반: Windows 기본 폰트·제목 표시줄, DPI 자동 배율</summary>
internal class ThemedForm : Form
{
    public ThemedForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.WindowBack;
        Icon = Theme.AppIcon;
    }

    /// <summary>96 DPI 기준 픽셀을 현재 DPI 픽셀로</summary>
    protected int Px(int logical) => LogicalToDeviceUnits(logical);
}
