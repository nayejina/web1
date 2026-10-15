using CloudConverter.App.Services;
using CloudConverter.App.UI;
using CloudConverter.Core;
using Microsoft.InformationProtection;
using Microsoft.InformationProtection.Exceptions;
using Microsoft.InformationProtection.File;
using MipLabel = Microsoft.InformationProtection.Label;

namespace CloudConverter.App.Mip;

/// <summary>
/// MIP SDK (File SDK) 로 실제 민감도 레이블과 보호를 적용하는 엔진.
///   MIP로 변환  : 선택한 레이블 적용 (레이블에 암호화가 설정되어 있으면 보호도 적용)
///   DRM으로 변환: MIP 보호 해제 (3rd party DRM 적용은 DRM 업체 SDK 연동이 필요해 아직 포함하지 않음)
/// 결과는 시연판과 같은 결과 코드로 돌려주므로 화면/로그/분류 파일은 그대로 사용한다.
/// </summary>
internal sealed class MipConversionEngine : IConversionEngine, IPreparableEngine, IDisposable
{
    private readonly MipSettings _settings;
    private readonly ExtensionRules _rules;
    private readonly ConversionPolicy _policy;
    private readonly string _dataDirectory;
    private MipAuthDelegate? _auth;
    private MipContext? _context;
    private IFileProfile? _profile;
    private IFileEngine? _engine;
    private MipLabel? _label;

    public MipConversionEngine(MipSettings settings, ExtensionRules rules, ConversionPolicy policy, string dataDirectory)
    {
        _settings = settings;
        _rules = rules;
        _policy = policy;
        _dataDirectory = dataDirectory;
    }

    public string? SignedInAccount => _auth?.Account;

    public Eligibility CheckEligibility(string filePath)
    {
        if (!_rules.IsAllowed(filePath))
            return new(false, ResultCodes.UnsupportedExtension);
        if (_policy.IsExcludedPath(filePath))
            return new(false, ResultCodes.ExcludedDirectory);
        return new(true, 0);
    }

    // ───────────────────────────── 준비: 로그인 / 레이블 선택 ─────────────────────────────

    public async Task<bool> PrepareAsync(ConversionFormat format, IWin32Window owner)
    {
        if (string.IsNullOrWhiteSpace(_settings.ClientId))
        {
            MessageDialog.Info(owner, "MIP 연동 설정이 없습니다.\nconfig\\mip-settings.json 에 Entra ID 앱의 ClientId 를 입력하세요.");
            return false;
        }

        if (_engine is null)
        {
            try
            {
                _auth ??= new MipAuthDelegate(_settings);
                var user = await _auth.SignInAsync();
                _engine = await Task.Run(() => CreateEngineAsync(user));
            }
            catch (Exception ex)
            {
                MessageDialog.Info(owner, "Microsoft 365 로그인 또는 MIP 초기화에 실패했습니다.\n\n" + Describe(ex));
                return false;
            }
        }

        if (format != ConversionFormat.Mip)
            return true;
        if (_label is not null && !_settings.AskLabelEveryTime)
            return true;

        var labels = FlattenLabels(_engine.SensitivityLabels).ToList();
        if (labels.Count == 0)
        {
            MessageDialog.Info(owner, "이 계정에 게시된 민감도 레이블이 없습니다.\nMicrosoft Purview 에서 레이블 정책을 확인하세요.");
            return false;
        }

        var configured = labels.FirstOrDefault(l => string.Equals(l.Label.Id, _settings.LabelId, StringComparison.OrdinalIgnoreCase));
        if (configured.Label is not null && !_settings.AskLabelEveryTime)
        {
            _label = configured.Label;
            return true;
        }

        var preselect = _label?.Id ?? configured.Label?.Id ?? _engine.DefaultSensitivityLabel?.Id;
        using var picker = new LabelPickerForm(labels, preselect);
        if (picker.ShowDialog(owner) != DialogResult.OK || picker.SelectedLabel is null)
            return false;
        _label = picker.SelectedLabel;
        return true;
    }

    private async Task<IFileEngine> CreateEngineAsync(string user)
    {
        // NuGet 패키지가 네이티브 DLL 을 출력 폴더의 x64 하위 폴더에 복사한다
        MIP.Initialize(MipComponent.File, Path.Combine(AppContext.BaseDirectory, "x64"));
        var appInfo = new ApplicationInfo
        {
            ApplicationId = _settings.ClientId,
            ApplicationName = _settings.ApplicationName,
            ApplicationVersion = typeof(MipConversionEngine).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
        };
        Directory.CreateDirectory(_dataDirectory);
        var configuration = new MipConfiguration(appInfo, _dataDirectory, ParseLogLevel(_settings.LogLevel), false, CacheStorageType.OnDiskEncrypted);
        _context = MIP.CreateMipContext(configuration);
        var profileSettings = new FileProfileSettings(_context, CacheStorageType.OnDiskEncrypted, new MipConsentDelegate());
        _profile = await MIP.LoadFileProfileAsync(profileSettings);
        var engineSettings = new FileEngineSettings(user, _auth!, "", "ko-KR") { Identity = new Identity(user) };
        return await _profile.AddEngineAsync(engineSettings);
    }

    /// <summary>하위 레이블이 있으면 "상위 / 하위" 로 펼친다 (하위가 있는 상위 레이블은 직접 적용 불가)</summary>
    private static IEnumerable<LabelChoice> FlattenLabels(IEnumerable<MipLabel> labels, string prefix = "")
    {
        foreach (var label in labels)
        {
            if (!label.IsActive) continue;
            var name = prefix.Length == 0 ? label.Name : prefix + " / " + label.Name;
            var children = label.Children?.ToList() ?? new List<MipLabel>();
            if (children.Count == 0)
                yield return new LabelChoice(label, name);
            else
                foreach (var child in FlattenLabels(children, name))
                    yield return child;
        }
    }

    private static LogLevel ParseLogLevel(string value) =>
        Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level) ? level : LogLevel.Warning;

    // ───────────────────────────── 파일 1개 변환 ─────────────────────────────

    public ConversionOutcome Convert(string sourcePath, string destinationPath, ConversionFormat format)
    {
        var engine = _engine;
        if (engine is null)
            return new(ResultCodes.MipError, "MIP 엔진이 준비되지 않았습니다 (로그인 필요)");
        if (!File.Exists(sourcePath))
            return new(ResultCodes.SourceNotFound);
        var eligibility = CheckEligibility(sourcePath);
        if (!eligibility.Convertible)
            return new(eligibility.ReasonCode);
        if (new FileInfo(sourcePath).Length == 0)
            return new(ResultCodes.EmptyFile);

        var directory = Path.GetDirectoryName(destinationPath)!;
        var temp = Path.Combine(directory, $"~ccmip_{Guid.NewGuid():N}_{Path.GetFileName(destinationPath)}");
        try
        {
            string finalPath;
            string detail;
            using (var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var handler = engine.CreateFileHandlerAsync(input, sourcePath, true).GetAwaiter().GetResult();
                var options = new LabelingOptions
                {
                    AssignmentMethod = AssignmentMethod.Standard,
                    IsDowngradeJustified = true,
                    JustificationMessage = _settings.JustificationMessage,
                };

                if (format == ConversionFormat.Mip)
                {
                    var label = _label ?? throw new InvalidOperationException("적용할 레이블이 선택되지 않았습니다");
                    if (handler.Protection is not null)
                        return new(ResultCodes.AlreadyEncrypted, $"보호 소유자: {handler.Protection.Owner}");
                    if (string.Equals(handler.Label?.Label?.Id, label.Id, StringComparison.OrdinalIgnoreCase))
                        return new(ResultCodes.AlreadyMip, $"이미 '{label.Name}' 레이블이 적용된 파일");
                    handler.SetLabel(label, options, new ProtectionSettings());
                    detail = $"적용한 레이블: {label.Name}";
                }
                else
                {
                    var removeLabel = _settings.RemoveLabelOnDrmConversion && handler.Label is not null;
                    if (handler.Protection is null && !removeLabel)
                        return new(ResultCodes.NotProtected);
                    if (handler.Protection is not null)
                        handler.RemoveProtection();
                    if (removeLabel)
                        handler.DeleteLabel(options);
                    detail = "MIP 보호 해제 (3rd party DRM 적용은 DRM 업체 SDK 연동 필요)";
                }

                // SDK 가 정한 출력 확장자 (예: .txt → .ptxt) 를 따른다. 파일명 번호((1) 등)는 유지
                finalPath = destinationPath;
                var sdkExtension = Path.GetExtension(handler.OutputFileName ?? "");
                if (sdkExtension.Length > 0 && !string.Equals(sdkExtension, Path.GetExtension(destinationPath), StringComparison.OrdinalIgnoreCase))
                    finalPath = Path.ChangeExtension(destinationPath, sdkExtension);

                Directory.CreateDirectory(directory);
                if (!handler.CommitAsync(temp).GetAwaiter().GetResult())
                    return new(ResultCodes.MipError, "변경 사항이 없어 저장하지 않았습니다");
                handler.NotifyCommitSuccessful(finalPath);
            }

            // 원본 스트림을 닫은 뒤 이동 (동일 폴더 저장 시 원본 자리에 덮어쓰기)
            File.Move(temp, finalPath, overwrite: true);
            return new(format == ConversionFormat.Mip ? ResultCodes.MipSuccess : ResultCodes.DrmSuccess, detail, finalPath);
        }
        catch (Exception ex)
        {
            TryDelete(temp);
            return MapException(ex);
        }
    }

    /// <summary>MIP SDK 예외 → 결과 코드 (log-classification.json 으로 분류/문구 변경 가능)</summary>
    private static ConversionOutcome MapException(Exception ex)
    {
        var message = Describe(ex);
        return ex switch
        {
            NoPermissionsException or AccessDeniedException or PrivilegedRequiredException
                => new(ResultCodes.DemoForcedFail, message),
            JustificationRequiredException => new(ResultCodes.JustificationRequired, message),
            NetworkException or ProxyAuthenticationException => new(ResultCodes.NetworkError, message),
            ContentFormatNotSupportedException or NotSupportedOperationException
                => new(ResultCodes.UnsupportedExtension, message),
            FileIOException or IOException or UnauthorizedAccessException => new(ResultCodes.FileAccessError, message),
            _ => new(ResultCodes.MipError, message),
        };
    }

    private static string Describe(Exception ex)
    {
        var inner = ex is AggregateException { InnerException: { } first } ? first : ex;
        return $"{inner.GetType().Name}: {inner.Message}";
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        _engine = null;
        _profile = null;
        try { _context?.ShutDown(); }
        catch (Exception) { }
        _context = null;
    }
}

internal readonly record struct LabelChoice(MipLabel Label, string DisplayName)
{
    public override string ToString() => DisplayName;
}
