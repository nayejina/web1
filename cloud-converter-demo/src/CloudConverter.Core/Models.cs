namespace CloudConverter.Core;

/// <summary>변환할 형식</summary>
public enum ConversionFormat
{
    Mip,
    Drm,
}

/// <summary>대상 파일 / 저장 위치 종류</summary>
public enum LocationKind
{
    MyDrive,
    OneDrive,
    SharePoint,
}

/// <summary>데모 상태 저장소에 기록되는 파일의 보호 상태</summary>
public enum ProtectionState
{
    None,
    Mip,
    Drm,
}

public static class LocationKindExtensions
{
    public static string DisplayName(this LocationKind kind) => kind switch
    {
        LocationKind.MyDrive => "내 드라이브",
        LocationKind.OneDrive => "OneDrive",
        LocationKind.SharePoint => "SharePoint",
        _ => kind.ToString(),
    };

    public static bool IsCloud(this LocationKind kind) => kind != LocationKind.MyDrive;
}

public static class ConversionFormatExtensions
{
    public static string DisplayName(this ConversionFormat format) => format switch
    {
        ConversionFormat.Mip => "MIP로 변환",
        ConversionFormat.Drm => "DRM으로 변환",
        _ => format.ToString(),
    };

    public static ProtectionState ResultState(this ConversionFormat format) =>
        format == ConversionFormat.Mip ? ProtectionState.Mip : ProtectionState.Drm;
}
