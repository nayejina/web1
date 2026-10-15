namespace CloudConverter.Core;

/// <summary>
/// 시연용 보호 상태 저장소. 실제로 암호화하지 않으므로, 이 프로그램이 "변환"한 파일을
/// 경로 + 크기 + 수정 시각으로 기억해 두었다가 다시 변환하면 "이미 암호화된 파일"로 처리한다.
/// 기록은 메모리에만 두므로 프로그램을 실행할 때마다 모든 파일이 암호화되지 않은 상태로 시작한다.
/// 파일이 수정되면 기록은 무효가 된다.
/// </summary>
public sealed class ProtectionStateStore
{
    private sealed record Entry(ProtectionState State, long Size, long LastWriteUtcTicks);

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new();

    private static string Key(string fullPath) => Path.GetFullPath(fullPath).ToUpperInvariant();

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
                _entries[Key(fullPath)] = new Entry(state, info.Length, info.LastWriteTimeUtc.Ticks);
        }
    }
}
