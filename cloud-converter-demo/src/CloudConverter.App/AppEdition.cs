namespace CloudConverter.App;

/// <summary>
/// 빌드 에디션. 같은 소스에서 두 개의 실행 파일을 만든다.
///   시연판(기본)   : CloudConverter.exe     - 오프라인, 실제 암호화 없음
///   MIP 버전       : CloudConverterMip.exe  - MIP SDK 로 실제 레이블/보호 적용 (dotnet publish -p:Edition=Mip)
/// 두 에디션은 우클릭 메뉴, 데이터 폴더, 단일 실행 식별자가 서로 달라 한 PC 에 같이 둘 수 있다.
/// </summary>
internal static class AppEdition
{
#if MIP_EDITION
    public static readonly bool IsDemo = false;
    public const string DataFolderName = "CloudConverterMip";
    public const string ContextMenuVerb = "CloudConverterMip";
    public const string ContextMenuText = "CloudConverter(MIP)";
    public const string InstanceName = "CloudConverterMip";
    public const string Notice = "Microsoft Information Protection SDK 로 실제 레이블과 보호를 적용합니다.";
#else
    public static readonly bool IsDemo = true;
    public const string DataFolderName = "CloudConverterDemo";
    public const string ContextMenuVerb = "CloudConverterDemo";
    public const string ContextMenuText = "CloudConverter(demo)";
    public const string InstanceName = "CloudConverterDemo";
    public const string Notice = Core.DemoSettings.Disclaimer;
#endif
}
