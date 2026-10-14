using System.Globalization;
using System.Text;

namespace CloudConverter.Core;

public sealed class LogEntry
{
    public DateTime Time { get; set; }
    public int Code { get; set; }
    public ConversionFormat Format { get; set; }
    public string FileName { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string DestinationPath { get; set; } = "";
    public string JobId { get; set; } = "";
}

/// <summary>
/// 원천 로그. 일자별 CSV (Logs\yyyyMMdd.csv) 로 저장한다.
/// 결과/분류 문구는 저장하지 않고 코드만 저장하므로, log-classification.json 을 바꾸면 과거 로그도 새 분류로 보인다.
/// </summary>
public sealed class LogStore
{
    private static readonly string[] Header = { "Time", "Code", "Format", "FileName", "SourcePath", "DestinationPath", "JobId" };
    private readonly object _gate = new();

    public string Directory { get; }

    public LogStore(string directory)
    {
        Directory = directory;
    }

    public void Append(LogEntry entry)
    {
        lock (_gate)
        {
            System.IO.Directory.CreateDirectory(Directory);
            var path = Path.Combine(Directory, entry.Time.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".csv");
            var isNew = !File.Exists(path);
            using var writer = new StreamWriter(path, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            if (isNew)
                writer.WriteLine(Csv.Line(Header));
            writer.WriteLine(Csv.Line(new[]
            {
                entry.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                entry.Code.ToString(CultureInfo.InvariantCulture),
                entry.Format.ToString(),
                entry.FileName,
                entry.SourcePath,
                entry.DestinationPath,
                entry.JobId,
            }));
        }
    }

    public List<LogEntry> Read(DateTime fromDate, DateTime toDate)
    {
        var result = new List<LogEntry>();
        lock (_gate)
        {
            if (!System.IO.Directory.Exists(Directory))
                return result;
            for (var day = fromDate.Date; day <= toDate.Date; day = day.AddDays(1))
            {
                var path = Path.Combine(Directory, day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".csv");
                if (!File.Exists(path))
                    continue;
                foreach (var fields in Csv.Parse(File.ReadAllText(path, Encoding.UTF8)).Skip(1))
                {
                    if (fields.Count < Header.Length)
                        continue;
                    if (!DateTime.TryParseExact(fields[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                        continue;
                    result.Add(new LogEntry
                    {
                        Time = time,
                        Code = int.TryParse(fields[1], out var code) ? code : 0,
                        Format = Enum.TryParse<ConversionFormat>(fields[2], out var format) ? format : ConversionFormat.Mip,
                        FileName = fields[3],
                        SourcePath = fields[4],
                        DestinationPath = fields[5],
                        JobId = fields[6],
                    });
                }
            }
        }
        return result;
    }

    /// <summary>로그 뷰어 "내보내기(CSV)": 분류 문구를 포함해 저장</summary>
    public static void Export(string path, IEnumerable<LogEntry> entries, LogClassification classification)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Csv.Line(new[] { "결과", "시간", "분류", "분류 코드", "변환 형식", "파일 이름", "파일 경로", "저장 경로", "부가 정보" }));
        foreach (var e in entries)
        {
            var rule = classification.Rule(e.Code);
            sb.AppendLine(Csv.Line(new[]
            {
                classification.Group(rule.Group).Display,
                e.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                rule.Category,
                e.Code.ToString(CultureInfo.InvariantCulture),
                e.Format.DisplayName(),
                e.FileName,
                e.SourcePath,
                e.DestinationPath,
                e.JobId,
            }));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}

public static class Csv
{
    public static string Line(IEnumerable<string> fields) =>
        string.Join(",", fields.Select(f =>
            f.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + f.Replace("\"", "\"\"") + "\"" : f));

    public static IEnumerable<List<string>> Parse(string text)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { fields.Add(field.ToString()); field.Clear(); }
            else if (c == '\r') { }
            else if (c == '\n')
            {
                fields.Add(field.ToString()); field.Clear();
                yield return fields;
                fields = new List<string>();
            }
            else if (c != '﻿') field.Append(c);
        }
        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            yield return fields;
        }
    }
}
