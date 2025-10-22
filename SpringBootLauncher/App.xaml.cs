using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;

namespace SpringBootLauncher
{
    public partial class App : Application
    {
        private static Mutex? _mutex;

        public App()
        {
            // 인코딩 프로바이더 등록 (CP949, EUC-KR 지원)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // CRITICAL: Set culture BEFORE any WPF initialization
            // This must be in constructor, not OnStartup
            try
            {
                var culture = new CultureInfo("en-US");
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;

                // Set as default for the entire AppDomain
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

                // Set default culture for all WPF elements
                FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("en-US")));
            }
            catch
            {
                // Fallback to invariant culture if en-US is not available
                var invariant = CultureInfo.InvariantCulture;
                Thread.CurrentThread.CurrentCulture = invariant;
                Thread.CurrentThread.CurrentUICulture = invariant;
                CultureInfo.DefaultThreadCurrentCulture = invariant;
                CultureInfo.DefaultThreadCurrentUICulture = invariant;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Single instance check using Mutex
            const string mutexName = "Global\\SpringBootLauncher_SingleInstance_Mutex";
            bool createdNew;

            try
            {
                _mutex = new Mutex(true, mutexName, out createdNew);

                if (!createdNew)
                {
                    // Another instance is already running
                    UniversalDialog.ShowInfo(null, "중복 실행 방지", "Spring Boot Launcher가 이미 실행 중입니다.\n트레이 아이콘을 확인하세요.");

                    Current.Shutdown();
                    return;
                }
            }
            catch (Exception ex)
            {
                UniversalDialog.ShowError(null, "오류", $"단일 인스턴스 확인 중 오류 발생: {ex.Message}");

                Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            var main = new MainWindow();

            // /minimized 또는 --minimized 인자 처리
            bool startMinimized = e.Args.Any(a =>
                a.Equals("/minimized", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

            if (startMinimized)
            {
                main.Loaded += (_, __) => main.MinimizeToTray(initial:true);
                main.Show();
            }
            else
            {
                main.Show();
            }

        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 서버 프로세스 강제 종료 (앱 종료 시 확실히 정리)
            try
            {
                var mainWindow = Current.MainWindow as MainWindow;
                mainWindow?.CleanupOnExit();
            }
            catch { /* Ignore */ }

            // Release mutex on exit
            if (_mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }
                catch { /* Ignore */ }
            }

            base.OnExit(e);
        }
    }
}