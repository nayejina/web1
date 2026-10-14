using CloudConverter.App.Forms;
using CloudConverter.App.Services;
using CloudConverter.App.UI;

namespace CloudConverter.App;

internal static class Program
{
    /// <summary>
    /// 실행 인자
    ///   (없음)                 바로가기로 실행: 빈 상태로 시작
    ///   --mip "파일" / --drm "파일"   탐색기 우클릭 메뉴로 실행: 형식이 선택되고 파일이 들어간 상태로 시작
    ///   --register / --unregister     우클릭 메뉴 등록 / 해제 후 종료
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Any(a => a.Equals("--register", StringComparison.OrdinalIgnoreCase)
                          || a.Equals("--unregister", StringComparison.OrdinalIgnoreCase)))
        {
            var extensions = new AppServices().Rules.AllowedExtensions;
            if (args.Any(a => a.Equals("--register", StringComparison.OrdinalIgnoreCase)))
                ContextMenuRegistrar.Register(extensions);
            else
                ContextMenuRegistrar.Unregister(extensions);
            return;
        }

        var request = LaunchRequest.Parse(args);
        using var instance = new SingleInstance();
        if (!instance.IsFirst)
        {
            // 이미 실행 중인 창으로 전달 (요청이 없으면 기존 창만 앞으로)
            SingleInstance.Send(request);
            return;
        }

        var services = new AppServices();
        if (services.Settings.AutoRegisterContextMenu)
        {
            try
            {
                if (!ContextMenuRegistrar.IsRegistered(services.Rules.AllowedExtensions))
                    ContextMenuRegistrar.Register(services.Rules.AllowedExtensions);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                services.ConfigErrors.Add("우클릭 메뉴 등록 실패: " + ex.Message);
            }
        }

        var form = new MainForm(services, request);
        instance.StartServer(r => form.EnqueueExternalRequest(r));
        Application.Run(form);
    }
}
