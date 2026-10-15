using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using CloudConverter.Core;

namespace CloudConverter.App.Services;

/// <summary>탐색기 우클릭으로 들어온 변환 요청</summary>
/// <param name="Format">null 이면 형식 지정 없이 기존 창만 활성화</param>
internal sealed record LaunchRequest(ConversionFormat? Format, IReadOnlyList<string> Paths)
{
    public static LaunchRequest Parse(IReadOnlyList<string> args)
    {
        ConversionFormat? format = null;
        var paths = new List<string>();
        foreach (var arg in args)
        {
            if (arg.Equals("--mip", StringComparison.OrdinalIgnoreCase)) format = ConversionFormat.Mip;
            else if (arg.Equals("--drm", StringComparison.OrdinalIgnoreCase)) format = ConversionFormat.Drm;
            else if (!arg.StartsWith("--")) paths.Add(arg);
        }
        return new LaunchRequest(format, paths);
    }

    public string Serialize() => (Format?.ToString() ?? "None") + "\n" + string.Join("\n", Paths);

    public static LaunchRequest? Deserialize(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return null;
        ConversionFormat? format = Enum.TryParse<ConversionFormat>(lines[0], out var parsed) ? parsed : null;
        return new LaunchRequest(format, lines.Skip(1).ToList());
    }
}

/// <summary>
/// 탐색기에서 여러 파일을 선택해 우클릭 실행하면 파일마다 프로세스가 실행된다.
/// 첫 프로세스만 창을 띄우고, 나머지는 이름 있는 파이프로 파일 경로를 전달한 뒤 종료한다.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private static readonly string Id = AppEdition.InstanceName + "_" + Environment.UserName;
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();

    public bool IsFirst { get; }

    public SingleInstance()
    {
        _mutex = new Mutex(true, @"Local\" + Id, out var createdNew);
        IsFirst = createdNew;
    }

    public void StartServer(Action<LaunchRequest> onRequest)
    {
        var thread = new Thread(() => ServerLoop(onRequest)) { IsBackground = true, Name = "CloudConverter pipe" };
        thread.Start();
    }

    private void ServerLoop(Action<LaunchRequest> onRequest)
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(Id, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                server.WaitForConnectionAsync(_cts.Token).GetAwaiter().GetResult();
                using var reader = new StreamReader(server, Encoding.UTF8);
                var request = LaunchRequest.Deserialize(reader.ReadToEnd());
                if (request is not null)
                    onRequest(request);
            }
            catch (OperationCanceledException) { return; }
            catch (IOException) { }
        }
    }

    /// <summary>이미 실행 중인 프로세스에 요청 전달. 첫 프로세스가 파이프를 여는 중일 수 있어 잠시 재시도한다.</summary>
    public static bool Send(LaunchRequest request)
    {
        AllowSetForegroundWindow(-1);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", Id, PipeDirection.Out);
                client.Connect(1000);
                var bytes = Encoding.UTF8.GetBytes(request.Serialize());
                client.Write(bytes, 0, bytes.Length);
                client.Flush();
                return true;
            }
            catch (TimeoutException) { }
            catch (IOException) { Thread.Sleep(200); }
        }
        return false;
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (IsFirst)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _mutex.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);
}
