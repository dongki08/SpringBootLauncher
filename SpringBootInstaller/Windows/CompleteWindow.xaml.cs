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
            SqlUserIdText.Text = config.SqlUserId;
            InstallPathText.Text = config.InstallPath;
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // SpringBootLauncher.exe 실행
                string launcherPath = Path.Combine(_config.InstallPath, "SpringBootLauncher.exe");

                if (File.Exists(launcherPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = launcherPath,
                        UseShellExecute = true,
                        WorkingDirectory = _config.InstallPath
                    });

                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show(
                        $"실행 파일을 찾을 수 없습니다.\n{launcherPath}",
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
