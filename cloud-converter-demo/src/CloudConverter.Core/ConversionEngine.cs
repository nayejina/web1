namespace CloudConverter.Core;

/// <summary>대상 파일 목록에 올라간 파일의 암호화 가능 여부</summary>
public readonly record struct Eligibility(bool Convertible, int ReasonCode);

/// <summary>
/// 시연용 변환 엔진. 실제 MIP/DRM 암호화는 하지 않고,
/// 정책·확장자 규칙에 따라 결과를 판정한 뒤 변환된 이름으로 파일을 복사한다.
/// </summary>
public sealed class ConversionEngine
{
    private static readonly byte[] CompoundFileSignature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
    private static readonly string[] OpenXmlExtensions =
        { ".docx", ".docm", ".dotx", ".xlsx", ".xlsm", ".xltx", ".pptx", ".pptm", ".potx", ".ppsx" };

    public ExtensionRules Rules { get; }
    public ConversionPolicy Policy { get; }
    public ProtectionStateStore States { get; }

    public ConversionEngine(ExtensionRules rules, ConversionPolicy policy, ProtectionStateStore states)
    {
        Rules = rules;
        Policy = policy;
        States = states;
    }

    /// <summary>목록의 "암호화" 컬럼(가능/불가) 판정. 관리 툴(정책)에서 허용하지 않는 것만 불가.</summary>
    public Eligibility CheckEligibility(string filePath)
    {
        if (!Rules.IsAllowed(filePath))
            return new(false, ResultCodes.UnsupportedExtension);
        if (Policy.IsExcludedPath(filePath))
            return new(false, ResultCodes.ExcludedDirectory);
        return new(true, 0);
    }

    /// <summary>파일 1개 변환. 반환값은 ResultCodes 의 코드</summary>
    public int Convert(string sourcePath, string destinationPath, ConversionFormat format)
    {
        try
        {
            var info = new FileInfo(sourcePath);
            if (!info.Exists)
                return ResultCodes.SourceNotFound;

            var eligibility = CheckEligibility(sourcePath);
            if (!eligibility.Convertible)
                return eligibility.ReasonCode;
            if (info.Length == 0)
                return ResultCodes.EmptyFile;

            var name = info.Name;
            if (ContainsAny(name, Policy.Demo.ForceSkipKeywords))
                return ResultCodes.PolicySkip;

            var state = States.Get(sourcePath);
            if (format == ConversionFormat.Mip)
            {
                if (state == ProtectionState.Mip)
                    return ResultCodes.AlreadyMip;
                if (Policy.Demo.DetectEncryptedFiles && LooksEncrypted(info))
                    return ResultCodes.AlreadyEncrypted;
            }
            else if (state == ProtectionState.Drm)
            {
                return ResultCodes.AlreadyDrm;
            }

            if (ContainsAny(name, Policy.Demo.ForceFailKeywords))
                return ResultCodes.DemoForcedFail;

            WriteOutput(info, destinationPath);
            States.Set(destinationPath, format.ResultState());
            return format == ConversionFormat.Mip ? ResultCodes.MipSuccess : ResultCodes.DrmSuccess;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ResultCodes.FileAccessError;
        }
    }

    private static void WriteOutput(FileInfo source, string destinationPath)
    {
        var sameFile = string.Equals(Path.GetFullPath(source.FullName), Path.GetFullPath(destinationPath),
            StringComparison.OrdinalIgnoreCase);
        if (!sameFile)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            var temp = destinationPath + ".ccdemo.tmp";
            File.Copy(source.FullName, temp, overwrite: true);
            File.Move(temp, destinationPath, overwrite: true);
        }
        File.SetLastWriteTime(destinationPath, DateTime.Now);
    }

    /// <summary>
    /// 실제로 암호화(MIP/IRM/암호)된 Office·PDF 파일 감지.
    /// 암호화된 OOXML 은 zip 이 아니라 OLE 복합 문서로 저장되고, 암호화된 PDF 는 트레일러에 /Encrypt 가 있다.
    /// </summary>
    public static bool LooksEncrypted(FileInfo info)
    {
        try
        {
            var ext = info.Extension.ToLowerInvariant();
            using var stream = info.OpenRead();
            if (OpenXmlExtensions.Contains(ext))
            {
                var header = new byte[8];
                return stream.Read(header, 0, 8) == 8 && header.AsSpan().SequenceEqual(CompoundFileSignature);
            }
            if (ext == ".pdf")
            {
                var length = (int)Math.Min(stream.Length, 64 * 1024);
                stream.Seek(-length, SeekOrigin.End);
                var buffer = new byte[length];
                stream.ReadExactly(buffer);
                return System.Text.Encoding.ASCII.GetString(buffer).Contains("/Encrypt");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return false;
    }

    private static bool ContainsAny(string name, IEnumerable<string> keywords) =>
        keywords.Any(k => !string.IsNullOrEmpty(k) && name.Contains(k, StringComparison.OrdinalIgnoreCase));
}
