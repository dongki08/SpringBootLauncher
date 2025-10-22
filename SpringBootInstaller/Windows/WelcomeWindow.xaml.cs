using System.Windows;

namespace SpringBootInstaller.Windows
{
    public partial class WelcomeWindow : Window
    {
        public WelcomeWindow()
        {
            InitializeComponent();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new SettingsWindow();

                // 메인 윈도 교체(앱 종료 방지)
                Application.Current.MainWindow = settingsWindow;

                settingsWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 화면을 여는 중 오류가 발생했습니다.\n\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "설치를 취소하시겠습니까?",
                "설치 취소",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }
    }
}
