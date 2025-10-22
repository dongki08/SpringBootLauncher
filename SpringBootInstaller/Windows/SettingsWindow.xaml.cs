using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SpringBootInstaller.Models;

namespace SpringBootInstaller.Windows
{
    public partial class SettingsWindow : Window
    {
        private bool _ready = false; // 로딩 완료 플래그
        public SettingsWindow()
        {
            InitializeComponent();

            // XAML 로딩 완료 후에만 이벤트 연결
            Loaded += (_, __) =>
            {
                // 여기서 이벤트 핸들러 연결 (XAML의 TextChanged/PasswordChanged 속성은 제거 권장)
                if (UserIdTextBox != null)        UserIdTextBox.TextChanged += OnAnyInputChanged;
                if (PasswordBox != null)          PasswordBox.PasswordChanged += OnAnyInputChanged;
                if (PasswordConfirmBox != null)   PasswordConfirmBox.PasswordChanged += OnAnyInputChanged;

                _ready = true;       // 이제부터 검증 로직 가동 허용
                ValidatePassword();  // 초기 1회 갱신
            };
        }
        private void OnAnyInputChanged(object sender, RoutedEventArgs e)
        {
            if (!_ready) return;        // 로딩 전에는 무시
            ValidatePassword();
        }

        private void PasswordBox_Changed(object sender, RoutedEventArgs e)
        {
            ValidatePassword();
        }

        private void UserIdTextBox_Changed(object sender, TextChangedEventArgs e)
        {
            ValidatePassword();
        }

        private void ValidatePassword()
        {
            // null-safe 가드
            if (PasswordBox == null || PasswordConfirmBox == null ||
                NextButton == null || UserIdTextBox == null)
                return;

            string password = PasswordBox.Password ?? string.Empty;
            string confirm  = PasswordConfirmBox.Password ?? string.Empty;

            bool passwordsMatch = !string.IsNullOrEmpty(password) && password == confirm;
            bool hasUserId      = !string.IsNullOrWhiteSpace(UserIdTextBox.Text);

            NextButton.IsEnabled = hasUserId && passwordsMatch;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "설치 경로 선택",
                FileName = "폴더 선택",
                Filter = "폴더|*.folder",
                CheckFileExists = false,
                CheckPathExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                string? selectedPath = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    InstallPathTextBox.Text = selectedPath;
                }
            }
        }

        private void BrowseScriptsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "DB 스크립트 폴더 선택",
                FileName = "폴더 선택",
                Filter = "폴더|*.folder",
                CheckFileExists = false,
                CheckPathExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                string? selectedPath = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    ScriptsPathTextBox.Text = selectedPath;
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var welcomeWindow = new WelcomeWindow();
            welcomeWindow.Show();
            this.Close();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // 설정 저장
            var config = new InstallConfig
            {
                SqlUserId = UserIdTextBox.Text.Trim(),
                SqlPassword = PasswordBox.Password,
                InstallPath = InstallPathTextBox.Text,
                ScriptsPath = string.IsNullOrWhiteSpace(ScriptsPathTextBox.Text) ? null : ScriptsPathTextBox.Text.Trim()
            };

            // 설치 진행 화면으로 이동
            var progressWindow = new ProgressWindow(config);
            progressWindow.Show();
            this.Close();
        }
    }
}
