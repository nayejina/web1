namespace CloudConverter.Core;

/// <summary>
/// 시연용 보호 상태 저장소. 실제로 암호화하지 않으므로, 이 프로그램이 "변환"한 파일을
/// 경로 + 크기 + 수정 시각으로 기억해 두었다가 다시 변환하면 "이미 암호화된 파일"로 처리한다.
/// 파일이 수정되면 기록은 무효가 된다.
/// </summary>
public sealed class ProtectionStateStore
{
    public sealed class Entry
    {
        public string Path { get; set; } = "";
        public ProtectionState State { get; set; }
        public long Size { get; set; }
        public long LastWriteUtcTicks { get; set; }
    }

    private readonly string _path;
    private readonly object _gate = new();
    private Dictionary<string, Entry> _entries;

    public ProtectionStateStore(string path)
    {
        _path = path;
        _entries = JsonConfig.Load(path, () => new Dictionary<string, Entry>());
    }

    private static string Key(string fullPath) => System.IO.Path.GetFullPath(fullPath).ToUpperInvariant();

    public ProtectionState Get(string fullPath)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(Key(fullPath), out var entry))
                return ProtectionState.None;
            var info = new FileInfo(fullPath);
            if (!info.Exists || info.Length != entry.Size || info.LastWriteTimeUtc.Ticks != entry.LastWriteUtcTicks)
                return ProtectionState.None;
            return entry.State;
        }
    }

    public void Set(string fullPath, ProtectionState state)
    {
        lock (_gate)
        {
            var info = new FileInfo(fullPath);
            if (state == ProtectionState.None || !info.Exists)
                _entries.Remove(Key(fullPath));
            else
                _entries[Key(fullPath)] = new Entry { Path = info.FullName, State = state, Size = info.Length, LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks };
            Save();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries = new();
            Save();
        }
    }

    private void Save()
    {
        try
        {
            // 존재하지 않는 파일 기록은 정리
            foreach (var (key, entry) in _entries.ToList())
            {
                if (!File.Exists(entry.Path))
                    _entries.Remove(key);
            }
            JsonConfig.Save(_path, _entries);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
