namespace CloudConverter.Core;

/// <summary>
/// 허용 확장자 및 MIP 보호 시 확장자 변환 규칙 (config/extension-rules.json).
/// MIP SDK "지원되는 파일 형식" 문서 기준:
///  - Office(Word/Excel/PowerPoint), PDF: 네이티브 보호, 확장자 유지
///  - 텍스트/이미지: 네이티브 보호, 확장자 앞에 p (.txt → .ptxt, .jpg → .pjpg ...)
///  - 그 외: 일반(generic) 보호, 원래 이름 뒤에 .pfile (report.zip → report.zip.pfile)
/// </summary>
public sealed class ExtensionRules
{
    /// <summary>변환 가능(암호화 가능) 확장자. 목록에 없으면 "불가"</summary>
    public List<string> AllowedExtensions { get; set; } = new();

    /// <summary>MIP 보호 시 원본 확장자 → 보호된 파일 확장자</summary>
    public Dictionary<string, string> MipProtectedExtensions { get; set; } = new();

    /// <summary>위 매핑에 없는 파일에 붙는 일반 보호 확장자</summary>
    public string GenericProtectedExtension { get; set; } = ".pfile";

    public static ExtensionRules CreateDefault() => new()
    {
        AllowedExtensions = new() { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf" },
        MipProtectedExtensions = new()
        {
            // Office - 네이티브 보호, 확장자 유지
            [".doc"] = ".doc", [".docm"] = ".docm", [".docx"] = ".docx", [".dot"] = ".dot",
            [".dotm"] = ".dotm", [".dotx"] = ".dotx",
            [".xls"] = ".xls", [".xlsb"] = ".xlsb", [".xlsm"] = ".xlsm", [".xlsx"] = ".xlsx",
            [".xlt"] = ".xlt", [".xltm"] = ".xltm", [".xltx"] = ".xltx", [".xla"] = ".xla", [".xlam"] = ".xlam",
            [".ppt"] = ".ppt", [".pptm"] = ".pptm", [".pptx"] = ".pptx", [".pps"] = ".pps",
            [".ppsm"] = ".ppsm", [".ppsx"] = ".ppsx", [".potm"] = ".potm", [".potx"] = ".potx",
            [".vsdx"] = ".vsdx", [".vsdm"] = ".vsdm", [".vssx"] = ".vssx", [".vssm"] = ".vssm",
            [".vstx"] = ".vstx", [".vstm"] = ".vstm", [".xps"] = ".xps", [".thmx"] = ".thmx",
            // PDF - 네이티브 보호, 확장자 유지 (구버전 .ppdf 아님)
            [".pdf"] = ".pdf",
            // 텍스트 / 이미지 - 네이티브 보호, p 접두
            [".txt"] = ".ptxt", [".xml"] = ".pxml", [".jpg"] = ".pjpg", [".jpeg"] = ".pjpeg",
            [".jpe"] = ".pjpe", [".jfif"] = ".pjfif", [".png"] = ".ppng", [".tif"] = ".ptif",
            [".tiff"] = ".ptiff", [".bmp"] = ".pbmp", [".gif"] = ".pgif", [".jt"] = ".pjt",
        },
    };

    public bool IsAllowed(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return AllowedExtensions.Any(a => string.Equals(Normalize(a), ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>MIP 보호 후 파일 이름</summary>
    public string GetMipProtectedFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        // JSON에서 읽은 Dictionary는 대소문자를 구분하므로 직접 비교한다
        foreach (var (original, protectedExt) in MipProtectedExtensions)
        {
            if (string.Equals(Normalize(original), ext, StringComparison.OrdinalIgnoreCase))
                return Path.GetFileNameWithoutExtension(fileName) + Normalize(protectedExt);
        }
        return fileName + Normalize(GenericProtectedExtension);
    }

    /// <summary>MIP 보호를 해제(DRM 변환)했을 때의 파일 이름. GetMipProtectedFileName의 역방향</summary>
    public string GetMipUnprotectedFileName(string fileName)
    {
        var generic = Normalize(GenericProtectedExtension);
        if (fileName.EndsWith(generic, StringComparison.OrdinalIgnoreCase) && fileName.Length > generic.Length)
            return fileName[..^generic.Length];

        var ext = Path.GetExtension(fileName);
        foreach (var (original, protectedExt) in MipProtectedExtensions)
        {
            if (!string.Equals(original, protectedExt, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Normalize(protectedExt), ext, StringComparison.OrdinalIgnoreCase))
                return Path.GetFileNameWithoutExtension(fileName) + Normalize(original);
        }
        return fileName;
    }

    /// <summary>변환 결과 파일 이름. DRM은 3rd party DRM 특성상 원본 확장자를 유지한다.</summary>
    public string GetOutputFileName(string fileName, ConversionFormat format) =>
        format == ConversionFormat.Mip ? GetMipProtectedFileName(fileName) : GetMipUnprotectedFileName(fileName);

    private static string Normalize(string ext) => ext.StartsWith('.') ? ext : "." + ext;
}
