using CloudConverter.Core;

namespace CloudConverter.Core.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ccdemo-" + Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public string File(params string[] parts) => System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());
    public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
}

public class ExtensionRulesTests
{
    private readonly ExtensionRules _rules = ExtensionRules.CreateDefault();

    [Theory]
    [InlineData("a.docx", "a.docx")]
    [InlineData("a.DOC", "a.doc")]
    [InlineData("a.xls", "a.xls")]
    [InlineData("a.pptx", "a.pptx")]
    [InlineData("a.pdf", "a.pdf")]
    [InlineData("a.txt", "a.ptxt")]
    [InlineData("a.jpg", "a.pjpg")]
    [InlineData("a.zip", "a.zip.pfile")]
    public void MipProtectedName(string input, string expected) =>
        Assert.Equal(expected, _rules.GetMipProtectedFileName(input));

    [Theory]
    [InlineData("a.ptxt", "a.txt")]
    [InlineData("a.zip.pfile", "a.zip")]
    [InlineData("a.docx", "a.docx")]
    public void DrmOutputReversesMip(string input, string expected) =>
        Assert.Equal(expected, _rules.GetOutputFileName(input, ConversionFormat.Drm));

    [Theory]
    [InlineData("x.doc", true)]
    [InlineData("x.XLSX", true)]
    [InlineData("x.pdf", true)]
    [InlineData("x.txt", false)]
    [InlineData("x.hwp", false)]
    [InlineData("noext", false)]
    public void Allowed(string name, bool expected) => Assert.Equal(expected, _rules.IsAllowed(name));

    [Fact]
    public void LoadedFromJsonIsCaseInsensitive()
    {
        using var dir = new TempDir();
        var path = dir.File("rules.json");
        JsonConfig.Save(path, ExtensionRules.CreateDefault());
        var loaded = JsonConfig.Load(path, ExtensionRules.CreateDefault);
        Assert.Equal("A.ptxt", loaded.GetMipProtectedFileName("A.TXT"));
    }
}

public class PolicyTests
{
    private readonly ConversionPolicy _policy = ConversionPolicy.CreateDefault();

    [Fact]
    public void MipRoutesMatchSpec()
    {
        Assert.Equal(new[] { LocationKind.MyDrive, LocationKind.OneDrive, LocationKind.SharePoint },
            _policy.TargetsFor(ConversionFormat.Mip, LocationKind.MyDrive));
        Assert.Equal(new[] { LocationKind.MyDrive }, _policy.TargetsFor(ConversionFormat.Mip, LocationKind.OneDrive));
        Assert.Equal(new[] { LocationKind.MyDrive }, _policy.SourcesForTarget(ConversionFormat.Mip, LocationKind.SharePoint));
    }

    [Fact]
    public void DrmAlwaysSavesToMyDrive()
    {
        foreach (var source in Enum.GetValues<LocationKind>())
            Assert.Equal(new[] { LocationKind.MyDrive }, _policy.TargetsFor(ConversionFormat.Drm, source));
    }

    [Fact]
    public void ExcludedKeyword()
    {
        var policy = ConversionPolicy.CreateDefault();
        policy.ExcludedDirectoryKeywords.Add("temp");
        Assert.True(policy.IsExcludedPath(Path.Combine("root", "MyTemp", "a.docx")));
        Assert.False(policy.IsExcludedPath(Path.Combine("root", "docs", "temp.docx")));
    }
}

public class DestinationPlannerTests
{
    private readonly ExtensionRules _rules = ExtensionRules.CreateDefault();

    [Fact]
    public void LocalToLocalOverwritesExistingButNumbersBatchDuplicates()
    {
        using var dir = new TempDir();
        var save = dir.File("save");
        Directory.CreateDirectory(save);
        File.WriteAllText(Path.Combine(save, "a.docx"), "old");
        var planner = new DestinationPlanner(_rules, ConversionFormat.Mip, LocationKind.MyDrive, LocationKind.MyDrive, save);

        Assert.Equal(Path.Combine(save, "a.docx"), planner.Plan(dir.File("x", "a.docx")));
        Assert.Equal(Path.Combine(save, "a(1).docx"), planner.Plan(dir.File("y", "a.docx")));
        Assert.Equal(Path.Combine(save, "a(2).docx"), planner.Plan(dir.File("z", "a.docx")));
    }

    [Fact]
    public void CloudKeepsExistingFiles()
    {
        using var dir = new TempDir();
        var save = dir.File("cloud");
        Directory.CreateDirectory(save);
        File.WriteAllText(Path.Combine(save, "a.docx"), "old");
        File.WriteAllText(Path.Combine(save, "a(1).docx"), "old");
        var planner = new DestinationPlanner(_rules, ConversionFormat.Mip, LocationKind.MyDrive, LocationKind.OneDrive, save);
        Assert.Equal(Path.Combine(save, "a(2).docx"), planner.Plan(dir.File("x", "a.docx")));
    }

    [Fact]
    public void SameFolderAndRelativeDirectory()
    {
        using var dir = new TempDir();
        var inPlace = new DestinationPlanner(_rules, ConversionFormat.Mip, LocationKind.MyDrive, LocationKind.MyDrive, null);
        Assert.Equal(dir.File("src", "a.txt").Replace("a.txt", "a.ptxt"), inPlace.Plan(dir.File("src", "a.txt")));

        var save = dir.File("save");
        var planner = new DestinationPlanner(_rules, ConversionFormat.Mip, LocationKind.MyDrive, LocationKind.MyDrive, save);
        Assert.Equal(Path.Combine(save, "폴더", "하위", "b.pdf"), planner.Plan(dir.File("폴더", "하위", "b.pdf"), Path.Combine("폴더", "하위")));
    }
}

public class EngineTests
{
    private static (ConversionEngine engine, TempDir dir) Create()
    {
        var dir = new TempDir();
        var engine = new ConversionEngine(ExtensionRules.CreateDefault(), ConversionPolicy.CreateDefault(),
            new ProtectionStateStore());
        return (engine, dir);
    }

    [Fact]
    public void ConvertThenAlreadyEncrypted()
    {
        var (engine, dir) = Create();
        using var _ = dir;
        var src = dir.File("a.docx");
        SampleFiles.CreateDocx(src, "hello");
        var dest = dir.File("out", "a.docx");

        Assert.Equal(ResultCodes.MipSuccess, engine.Convert(src, dest, ConversionFormat.Mip));
        Assert.True(File.Exists(dest));
        Assert.Equal(File.ReadAllBytes(src), File.ReadAllBytes(dest));
        Assert.Equal(ResultCodes.AlreadyMip, engine.Convert(dest, dir.File("out2", "a.docx"), ConversionFormat.Mip));

        // MIP 파일은 DRM 으로 변환 가능, DRM 파일을 다시 DRM 변환하면 실패
        var drm = dir.File("drm", "a.docx");
        Assert.Equal(ResultCodes.DrmSuccess, engine.Convert(dest, drm, ConversionFormat.Drm));
        Assert.Equal(ResultCodes.AlreadyDrm, engine.Convert(drm, dir.File("drm2", "a.docx"), ConversionFormat.Drm));
    }

    [Fact]
    public void NewSessionStartsUnencrypted()
    {
        var (engine, dir) = Create();
        using var _ = dir;
        var src = dir.File("a.pdf");
        SampleFiles.CreatePdf(src, "x");
        Assert.Equal(ResultCodes.MipSuccess, engine.Convert(src, src, ConversionFormat.Mip));
        Assert.Equal(ResultCodes.AlreadyMip, engine.Convert(src, src, ConversionFormat.Mip));

        // 프로그램을 다시 실행한 것과 같음: 새 저장소에서는 다시 변환 가능
        var next = new ConversionEngine(ExtensionRules.CreateDefault(), ConversionPolicy.CreateDefault(), new ProtectionStateStore());
        Assert.Equal(ResultCodes.MipSuccess, next.Convert(src, src, ConversionFormat.Mip));
    }

    [Fact]
    public void SkipAndFailRules()
    {
        var (engine, dir) = Create();
        using var _ = dir;
        var empty = dir.File("empty.docx");
        File.WriteAllBytes(empty, Array.Empty<byte>());
        var txt = dir.File("a.txt");
        File.WriteAllText(txt, "x");
        var encrypted = dir.File("enc.docx");
        SampleFiles.CreateEncryptedLookingOffice(encrypted);
        var forced = dir.File("b_fail.docx");
        SampleFiles.CreateDocx(forced, "x");
        var skip = dir.File("c_skip.xlsx");
        SampleFiles.CreateXlsx(skip, new[] { new[] { "a" } });

        Assert.Equal(ResultCodes.EmptyFile, engine.Convert(empty, dir.File("o", "1.docx"), ConversionFormat.Mip));
        Assert.Equal(ResultCodes.UnsupportedExtension, engine.Convert(txt, dir.File("o", "a.ptxt"), ConversionFormat.Mip));
        Assert.Equal(ResultCodes.AlreadyEncrypted, engine.Convert(encrypted, dir.File("o", "2.docx"), ConversionFormat.Mip));
        Assert.Equal(ResultCodes.DemoForcedFail, engine.Convert(forced, dir.File("o", "3.docx"), ConversionFormat.Mip));
        Assert.Equal(ResultCodes.PolicySkip, engine.Convert(skip, dir.File("o", "4.xlsx"), ConversionFormat.Mip));
        Assert.Equal(ResultCodes.SourceNotFound, engine.Convert(dir.File("none.docx"), dir.File("o", "5.docx"), ConversionFormat.Mip));
        Assert.False(Directory.Exists(dir.File("o")));
    }

    [Fact]
    public void EncryptedPdfDetected()
    {
        using var dir = new TempDir();
        var pdf = dir.File("a.pdf");
        File.WriteAllText(pdf, "%PDF-1.4\ntrailer << /Encrypt 5 0 R >>\n%%EOF");
        Assert.True(ConversionEngine.LooksEncrypted(new FileInfo(pdf)));
        SampleFiles.CreatePdf(pdf, "plain");
        Assert.False(ConversionEngine.LooksEncrypted(new FileInfo(pdf)));
    }
}

public class LogTests
{
    [Fact]
    public void AppendReadExportRoundTrip()
    {
        using var dir = new TempDir();
        var store = new LogStore(dir.File("logs"));
        var now = new DateTime(2025, 12, 9, 14, 30, 0);
        store.Append(new LogEntry { Time = now, Code = ResultCodes.AlreadyMip, FileName = "a,\"b\".docx", SourcePath = "C:\\x\\a.docx", JobId = "j1" });
        store.Append(new LogEntry { Time = now.AddDays(1), Code = ResultCodes.MipSuccess, FileName = "c.docx" });

        var entries = store.Read(now.Date, now.Date);
        var e = Assert.Single(entries);
        Assert.Equal("a,\"b\".docx", e.FileName);
        Assert.Equal(ResultCodes.AlreadyMip, e.Code);
        Assert.Equal(2, store.Read(now.Date, now.Date.AddDays(1)).Count);

        var export = dir.File("export.csv");
        LogStore.Export(export, entries, LogClassification.CreateDefault());
        var rows = Csv.Parse(File.ReadAllText(export)).ToList();
        Assert.Equal("실패", rows[1][0]);
        Assert.Equal("이미 암호화된 파일 (시연 변환 기록)", rows[1][2]);
    }

    [Fact]
    public void ClassificationFileOverridesGroups()
    {
        using var dir = new TempDir();
        var custom = LogClassification.CreateDefault();
        custom.Groups.Add(new ResultGroup { Id = "hold", Display = "보류", Style = "Skip" });
        custom.Codes.First(c => c.Code == ResultCodes.AlreadyMip).Group = "hold";
        var path = dir.File("log-classification.json");
        JsonConfig.Save(path, custom);

        var loaded = JsonConfig.Load(path, LogClassification.CreateDefault);
        Assert.Equal("보류", loaded.GroupForCode(ResultCodes.AlreadyMip).Display);
        Assert.Equal("코드 77", loaded.Rule(77).Category);
    }
}

public class SampleTests
{
    [Fact]
    public void DemoContentCreatedOnce()
    {
        using var dir = new TempDir();
        var settings = new DemoSettings();
        SampleFiles.EnsureDemoContent(dir.File("cloud"), settings, dir.File("samples"));
        Assert.True(File.Exists(dir.File("cloud", "OneDrive", "문서", "2025 사업계획서.docx")));
        Assert.True(File.Exists(dir.File("cloud", "SharePoint", "영업팀", "공유 문서", "Security Policy.pdf")));
        Assert.True(File.Exists(dir.File("samples", "프로젝트 폴더", "산출물", "테스트 결과서.docx")));
        using var zip = System.IO.Compression.ZipFile.OpenRead(dir.File("samples", "견적서.xlsx"));
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
    }
}

public class DemoSampleTests
{
    [Fact]
    public void DetectionCanBeDisabled()
    {
        using var dir = new TempDir();
        var policy = ConversionPolicy.CreateDefault();
        policy.Demo.DetectEncryptedFiles = false;
        var engine = new ConversionEngine(ExtensionRules.CreateDefault(), policy, new ProtectionStateStore());
        var encrypted = dir.File("enc.docx");
        SampleFiles.CreateEncryptedLookingOffice(encrypted);
        Assert.Equal(ResultCodes.MipSuccess, engine.Convert(encrypted, dir.File("o", "enc.docx"), ConversionFormat.Mip));
    }

    [Fact]
    public void FreshSamplesConvertAsDocumented()
    {
        using var dir = new TempDir();
        var engine = new ConversionEngine(ExtensionRules.CreateDefault(), ConversionPolicy.CreateDefault(),
            new ProtectionStateStore());
        var samples = dir.File("samples");
        SampleFiles.EnsureDemoContent(dir.File("cloud"), new DemoSettings(), samples);
        int Run(string name) => engine.Convert(Path.Combine(samples, name), dir.File("out", name), ConversionFormat.Mip);

        Assert.Equal(ResultCodes.MipSuccess, Run("제안서_초안.docx"));
        Assert.Equal(ResultCodes.MipSuccess, Run("견적서.xlsx"));
        Assert.Equal(ResultCodes.MipSuccess, Run("Company Profile.pdf"));
        Assert.Equal(ResultCodes.AlreadyEncrypted, Run("이미 암호화된 보고서.docx"));
        Assert.Equal(ResultCodes.EmptyFile, Run("빈 파일.docx"));
        Assert.Equal(ResultCodes.DemoForcedFail, Run("권한없음_fail.docx"));
        Assert.Equal(ResultCodes.UnsupportedExtension, Run("readme.txt"));
    }
}
