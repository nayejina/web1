using Microsoft.Identity.Client;
using Microsoft.InformationProtection;

namespace CloudConverter.App.Mip;

/// <summary>
/// MSAL 로 Microsoft 365 에 로그인하고, MIP SDK 가 요청하는 서비스(정책/보호)용 토큰을 발급한다.
/// 대화형 로그인은 기본 브라우저를 사용한다 (앱 등록에 리디렉션 URI http://localhost 필요).
/// </summary>
internal sealed class MipAuthDelegate : IAuthDelegate
{
    private static readonly string[] SignInScopes = { "User.Read" };
    private readonly IPublicClientApplication _app;
    private IAccount? _account;

    public MipAuthDelegate(MipSettings settings)
    {
        var tenant = string.IsNullOrWhiteSpace(settings.TenantId) ? "organizations" : settings.TenantId;
        _app = PublicClientApplicationBuilder.Create(settings.ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenant)
            .WithRedirectUri(settings.RedirectUri)
            .Build();
    }

    public string? Account => _account?.Username;

    /// <summary>처음 한 번 로그인해 계정을 정한다 (UI 스레드에서 호출)</summary>
    public async Task<string> SignInAsync()
    {
        var existing = (await _app.GetAccountsAsync()).FirstOrDefault();
        AuthenticationResult result;
        try
        {
            result = await _app.AcquireTokenSilent(SignInScopes, existing).ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            result = await _app.AcquireTokenInteractive(SignInScopes)
                .WithUseEmbeddedWebView(false)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync();
        }
        _account = result.Account;
        return result.Account.Username;
    }

    /// <summary>MIP SDK 콜백 (SDK 스레드에서 동기 호출됨)</summary>
    public string AcquireToken(Identity identity, string authority, string resource, string claims)
    {
        var scope = resource.EndsWith('/') ? resource + ".default" : resource + "/.default";
        var scopes = new[] { scope };
        if (string.IsNullOrEmpty(claims) && _account is not null)
        {
            try
            {
                return _app.AcquireTokenSilent(scopes, _account).ExecuteAsync().GetAwaiter().GetResult().AccessToken;
            }
            catch (MsalUiRequiredException) { }
        }

        var interactive = _app.AcquireTokenInteractive(scopes).WithUseEmbeddedWebView(false);
        if (_account is not null) interactive = interactive.WithAccount(_account);
        if (!string.IsNullOrEmpty(claims)) interactive = interactive.WithClaims(claims);
        var result = interactive.ExecuteAsync().GetAwaiter().GetResult();
        _account = result.Account;
        return result.AccessToken;
    }
}

/// <summary>MIP 서비스 연결 동의. 회사 앱이므로 자동 동의</summary>
internal sealed class MipConsentDelegate : IConsentDelegate
{
    public Consent GetUserConsent(string url) => Consent.Accept;
}
