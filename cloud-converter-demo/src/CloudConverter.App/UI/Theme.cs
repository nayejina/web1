using System.Runtime.InteropServices;

namespace CloudConverter.App.UI;

/// <summary>디자인 가이드(2) - Purview 와 유사한 색상 배치</summary>
internal static class Theme
{
    // 제목 표시줄 및 메인 메뉴
    public static readonly Color HeaderBack = ColorTranslator.FromHtml("#004E8C");
    public static readonly Color HeaderText = Color.White;
    public static readonly Color HeaderTextDisabled = ColorTranslator.FromHtml("#ADC6DA");

    // <변환 시작> 버튼
    public static readonly Color PrimaryBack = Color.White;
    public static readonly Color PrimaryText = Color.Black;
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
    public static readonly Color ProgressBar = ColorTranslator.FromHtml("#4A8FD9");
    public static readonly Color ProgressTrack = Color.White;
    public static readonly Color ProgressBorder = ColorTranslator.FromHtml("#9AA7B4");
    public static readonly Color ProgressFail = ColorTranslator.FromHtml("#C9C9C9");
    public static readonly Color ProgressSkip = ColorTranslator.FromHtml("#B9D7F4");
    public static readonly Color Accent = ColorTranslator.FromHtml("#0078D4");
    public static readonly Color Warning = ColorTranslator.FromHtml("#C42B1C");

    public const string FontName = "Malgun Gothic";

    public static Font Regular(float size = 9f) => new(FontName, size, FontStyle.Regular);
    public static Font Bold(float size = 9f) => new(FontName, size, FontStyle.Bold);

    /// <summary>Windows 11 제목 표시줄 색상을 Purview 색상으로 지정</summary>
    public static void ApplyTitleBar(Form form)
    {
        try
        {
            var caption = ColorTranslator.ToWin32(HeaderBack);
            var text = ColorTranslator.ToWin32(HeaderText);
            DwmSetWindowAttribute(form.Handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            DwmSetWindowAttribute(form.Handle, DWMWA_TEXT_COLOR, ref text, sizeof(int));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static Icon AppIcon
    {
        get
        {
            using var stream = typeof(Theme).Assembly.GetManifestResourceStream("app.ico");
            return stream is null ? SystemIcons.Application : new Icon(stream);
        }
    }
}

/// <summary>모든 창의 공통 기반: 맑은 고딕, DPI 자동 배율, 제목 표시줄 색상</summary>
internal class ThemedForm : Form
{
    public ThemedForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Theme.Regular();
        BackColor = Theme.WindowBack;
        Icon = Theme.AppIcon;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
    }

    /// <summary>96 DPI 기준 픽셀을 현재 DPI 픽셀로</summary>
    protected int Px(int logical) => LogicalToDeviceUnits(logical);
}
