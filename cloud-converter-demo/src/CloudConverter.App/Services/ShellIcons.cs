using System.Runtime.InteropServices;

namespace CloudConverter.App.Services;

/// <summary>확장자별 Windows 기본 아이콘 (탐색기와 동일한 아이콘)</summary>
internal sealed class ShellIcons
{
    public const string FolderKey = "<folder>";
    private readonly ImageList _images;

    public ShellIcons(ImageList images)
    {
        _images = images;
    }

    public string KeyFor(string path, bool isFolder)
    {
        var key = isFolder ? FolderKey : Path.GetExtension(path).ToLowerInvariant();
        if (key.Length == 0) key = "<file>";
        if (_images.Images.ContainsKey(key))
            return key;

        var icon = Load(isFolder ? "folder" : "file" + key, isFolder);
        if (icon is not null)
        {
            _images.Images.Add(key, icon);
        }
        else
        {
            _images.Images.Add(key, isFolder ? SystemIcons.WinLogo : SystemIcons.Application);
        }
        return key;
    }

    private static Icon? Load(string name, bool isFolder)
    {
        try
        {
            var info = new SHFILEINFO();
            var attributes = isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
            var result = SHGetFileInfo(name, attributes, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(),
                SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
                return null;
            using var shared = Icon.FromHandle(info.hIcon);
            var copy = (Icon)shared.Clone();
            DestroyIcon(info.hIcon);
            return copy;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or ExternalException)
        {
            return null;
        }
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_SMALLICON = 0x1;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
