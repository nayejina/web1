using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudConverter.Core;

/// <summary>config 폴더의 JSON 파일을 읽는다. 파일이 없거나 깨졌으면 코드에 정의된 기본값을 사용한다.</summary>
public static class JsonConfig
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T Load<T>(string path, Func<T> fallback, Action<string>? onError = null)
    {
        try
        {
            if (!File.Exists(path))
                return fallback();
            var value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
            return value ?? fallback();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            onError?.Invoke($"{Path.GetFileName(path)}: {ex.Message}");
            return fallback();
        }
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
    }
}
