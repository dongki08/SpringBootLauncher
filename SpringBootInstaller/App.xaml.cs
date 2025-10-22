using System.Windows;

namespace SpringBootInstaller
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 앱 종료 모드: 마지막 창이 닫힐 때만 종료
            Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
            
            // 관리자 권한 확인
            if (!IsAdministrator())
            {
                MessageBox.Show(
                    "이 프로그램은 관리자 권한이 필요합니다.\n프로그램을 종료합니다.",
                    "권한 필요",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                Shutdown();
            }
            
            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(args.Exception.ToString(), "UI Thread Exception");
                args.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                MessageBox.Show(args.ExceptionObject.ToString(), "Unhandled Exception");
            };
            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                MessageBox.Show(args.Exception.ToString(), "Task Exception");
                args.SetObserved();
            };
        }

        private bool IsAdministrator()
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        
    }
}
