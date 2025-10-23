using System.Diagnostics;
using System.IO;
using System.Windows;
using SpringBootInstaller.Models;

namespace SpringBootInstaller.Windows
{
    public partial class CompleteWindow : Window
    {
        private readonly InstallConfig _config;

        public CompleteWindow(InstallConfig config)
        {
            InitializeComponent();
            _config = config;

            // 설치 정보 표시
            SqlServerText.Text = config.SqlServer;
            AppUserIdText.Text = config.AppUserId;
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 고정 경로: C:\ACS\server\Launcher\SpringBootLauncher.exe
                string launcherFolder = @"C:\ACS\server\Launcher";
                string launcherPath = Path.Combine(launcherFolder, "SpringBootLauncher.exe");

                if (File.Exists(launcherPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = launcherPath,
                        UseShellExecute = true,
                        WorkingDirectory = launcherFolder
                    });

                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show(
                        $"실행 파일을 찾을 수 없습니다.\n{launcherPath}\n\nSpringBootLauncher.exe가 C:\\ACS\\server\\Launcher\\ 폴더에 있어야 합니다.",
                        "오류",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"프로그램 실행 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
