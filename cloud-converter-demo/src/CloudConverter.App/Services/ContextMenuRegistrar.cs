using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CloudConverter.App.Services;

/// <summary>
/// Windows 탐색기 우클릭 메뉴 (기획서 22p, V1.4)
///   CloudConverter(demo) ▶ MIP로 변환 / DRM으로 변환
/// HKCU 에 등록하므로 관리자 권한이 필요 없다.
/// Windows 11 에서는 "더 많은 옵션 표시"(Shift+F10) 메뉴에 나타난다.
/// </summary>
internal static class ContextMenuRegistrar
{
    private const string VerbName = "CloudConverterDemo";
    /// <summary>메뉴에 보이는 이름. 실제 CloudConverter 가 설치된 PC 에서도 구분되도록 (Demo) 를 붙인다</summary>
    private const string MenuText = "CloudConverter(demo)";
    private const string ClassesRoot = @"Software\Classes";

    private static IEnumerable<string> ShellKeys(IEnumerable<string> extensions)
    {
        foreach (var ext in extensions)
        {
            var normalized = ext.StartsWith('.') ? ext : "." + ext;
            yield return $@"{ClassesRoot}\SystemFileAssociations\{normalized}\shell\{VerbName}";
        }
        yield return $@"{ClassesRoot}\Directory\shell\{VerbName}";
    }

    private static string Command(string exe, string format) => $"\"{exe}\" --{format} \"%1\"";

    public static bool IsRegistered(IEnumerable<string> extensions)
    {
        var exe = AppServices.ExecutablePath;
        return ShellKeys(extensions).All(key =>
        {
            // 메뉴 이름이 바뀐 이전 버전 등록도 다시 등록되도록 이름까지 확인
            using var verb = Registry.CurrentUser.OpenSubKey(key);
            using var command = Registry.CurrentUser.OpenSubKey(key + @"\shell\01mip\command");
            return string.Equals(verb?.GetValue("MUIVerb") as string, MenuText, StringComparison.Ordinal)
                   && string.Equals(command?.GetValue(null) as string, Command(exe, "mip"), StringComparison.OrdinalIgnoreCase);
        });
    }

    public static void Register(IEnumerable<string> extensions)
    {
        var exe = AppServices.ExecutablePath;
        Unregister(extensions);
        foreach (var keyPath in ShellKeys(extensions))
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue("MUIVerb", MenuText);
            key.SetValue("Icon", $"\"{exe}\",0");
            key.SetValue("SubCommands", "");
            key.SetValue("MultiSelectModel", "Player");

            AddSubCommand(key, "01mip", "MIP로 변환", Command(exe, "mip"), exe);
            AddSubCommand(key, "02drm", "DRM으로 변환", Command(exe, "drm"), exe);
        }
        NotifyShell();
    }

    public static void Unregister(IEnumerable<string> extensions)
    {
        // 현재 확장자 목록 외에 예전에 등록했던 확장자도 정리
        var all = extensions.Concat(new[] { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf" })
            .Select(e => e.ToLowerInvariant()).Distinct();
        foreach (var keyPath in ShellKeys(all))
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        NotifyShell();
    }

    private static void AddSubCommand(RegistryKey parent, string name, string text, string command, string exe)
    {
        using var verb = parent.CreateSubKey($@"shell\{name}");
        verb.SetValue("MUIVerb", text);
        verb.SetValue("Icon", $"\"{exe}\",0");
        verb.SetValue("MultiSelectModel", "Player");
        using var cmd = verb.CreateSubKey("command");
        cmd.SetValue(null, command);
    }

    private static void NotifyShell()
    {
        try { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); }
        catch (DllNotFoundException) { }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
