using System.Runtime.InteropServices;

namespace CloudConverter.App.Services;

/// <summary>확장자별 Windows 기본 아이콘 (탐색기와 동일한 아이콘)</summary>
internal sealed class ShellIcons
{
    public const string FolderKey = "<folder>";
    private readonly ImageList _images;
    private readonly int _padding;

    /// <param name="padding">목록 행 높이를 키우기 위해 아이콘 위아래에 둘 여백(px). 이미지 크기는 ImageList.ImageSize</param>
    public ShellIcons(ImageList images, int padding = 0)
    {
        _images = images;
        _padding = padding;
    }

    public string KeyFor(string path, bool isFolder)
    {
        var key = isFolder ? FolderKey : Path.GetExtension(path).ToLowerInvariant();
        if (key.Length == 0) key = "<file>";
        if (_images.Images.ContainsKey(key))
            return key;

        var icon = Load(isFolder ? "folder" : "file" + key, isFolder);
        icon ??= (Icon)(isFolder ? SystemIcons.WinLogo : SystemIcons.Application).Clone();
        if (_padding == 0)
        {
            _images.Images.Add(key, icon);
            return key;
        }
        var size = _images.ImageSize;
        var side = Math.Max(1, size.Height - 2 * _padding);
        var bitmap = new Bitmap(size.Width, size.Height);
        using (var g = Graphics.FromImage(bitmap))
            g.DrawIcon(icon, new Rectangle(0, _padding, side, side));
        icon.Dispose();
        _images.Images.Add(key, bitmap);
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
