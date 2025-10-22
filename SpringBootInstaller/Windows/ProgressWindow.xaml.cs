using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpringBootInstaller.Models;
using SpringBootInstaller.Services;

namespace SpringBootInstaller.Windows
{
    public partial class ProgressWindow : Window
    {
        private readonly InstallConfig _config;
        private readonly List<TaskItem> _tasks;
        private bool _isInstalling = false;
        private bool _installCompleted = false;

        public ProgressWindow(InstallConfig config)
        {
            InitializeComponent();
            _config = config;
            _tasks = new List<TaskItem>();

            InitializeTasks();

            // 로그 이벤트 구독
            LogManager.Instance.LogAdded += OnLogAdded;

            _ = StartInstallation();
        }

        private void OnLogAdded(string level, string message)
        {
            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string logLine = $"[{timestamp}] [{level}] {message}";

                // 로그 추가
                LogTextBlock.Text += logLine + Environment.NewLine;

                // 자동 스크롤 (최신 로그가 보이도록)
                LogScrollViewer.ScrollToBottom();
            });
        }

        private void InitializeTasks()
        {
            // 작업 목록 초기화
            var taskNames = new[]
            {
                "관리자 권한 확인",
                "OpenJDK 17 설치 (C:\\java)",
                "환경변수 설정 (JAVA_HOME, PATH)",
                "MSSQL Developer 설치",
                "SQL Server 서비스 시작 대기",
                "데이터베이스 스크립트 실행",
                "애플리케이션 설치",
                "바탕화면 바로가기 생성"
            };

            foreach (var taskName in taskNames)
            {
                var taskItem = new TaskItem { Name = taskName, Status = TaskStatus.Pending };
                _tasks.Add(taskItem);

                var taskPanel = CreateTaskPanel(taskItem);
                TaskListPanel.Children.Add(taskPanel);
            }
        }

        private StackPanel CreateTaskPanel(TaskItem task)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 8)
            };

            var icon = new TextBlock
            {
                Text = "⏸",
                FontSize = 16,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            task.IconTextBlock = icon;

            var text = new TextBlock
            {
                Text = task.Name,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                VerticalAlignment = VerticalAlignment.Center
            };
            task.NameTextBlock = text;

            panel.Children.Add(icon);
            panel.Children.Add(text);

            return panel;
        }

        private async Task StartInstallation()
        {
            _isInstalling = true;

            try
            {
                var installer = new InstallService(_config);

                // 진행 상황 업데이트 이벤트 구독
                installer.ProgressChanged += (progress, message) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateProgress(progress, message);
                    });
                };

                installer.TaskStatusChanged += (taskIndex, status) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateTaskStatus(taskIndex, status);
                    });
                };

                // 설치 실행
                bool success = await installer.InstallAsync();

                _isInstalling = false;
                _installCompleted = true;

                if (success)
                {
                    // 완료 화면으로 이동
                    var completeWindow = new CompleteWindow(_config);
                    completeWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show(
                        "설치 중 오류가 발생했습니다.\n로그 파일을 확인하세요.\n\nC:\\MyAppInstall\\Logs\\",
                        "설치 실패",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                _isInstalling = false;
                MessageBox.Show(
                    $"설치 중 예외가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void UpdateProgress(int percentage, string message)
        {
            // 진행률 업데이트
            ProgressPercentText.Text = $"{percentage}%";

            // ProgressBarContainer의 실제 크기를 기준으로 계산
            if (ProgressBarContainer.ActualWidth > 0)
            {
                ProgressBar.Width = Math.Max(0, ProgressBarContainer.ActualWidth * percentage / 100);
            }

            // 현재 작업 메시지 업데이트
            CurrentTaskText.Text = message;
        }

        private void UpdateTaskStatus(int taskIndex, TaskStatus status)
        {
            if (taskIndex < 0 || taskIndex >= _tasks.Count)
                return;

            var task = _tasks[taskIndex];
            task.Status = status;

            // 아이콘 및 색상 업데이트
            switch (status)
            {
                case TaskStatus.InProgress:
                    task.IconTextBlock.Text = "⏳";
                    task.NameTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)); // Blue
                    task.NameTextBlock.FontWeight = FontWeights.SemiBold;
                    break;

                case TaskStatus.Completed:
                    task.IconTextBlock.Text = "✓";
                    task.NameTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(13, 150, 104)); // Green
                    task.NameTextBlock.FontWeight = FontWeights.Normal;
                    break;

                case TaskStatus.Failed:
                    task.IconTextBlock.Text = "❌";
                    task.NameTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Red
                    task.NameTextBlock.FontWeight = FontWeights.SemiBold;
                    break;

                default: // Pending
                    task.IconTextBlock.Text = "⏸";
                    task.NameTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // Gray
                    task.NameTextBlock.FontWeight = FontWeights.Normal;
                    break;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_installCompleted)
            {
                Application.Current.Shutdown();
                return;
            }

            var result = MessageBox.Show(
                "설치를 취소하시겠습니까?\n진행 중인 작업이 중단됩니다.",
                "설치 취소",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                // TODO: 설치 중단 및 롤백 로직
                Application.Current.Shutdown();
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_isInstalling && !_installCompleted)
            {
                var result = MessageBox.Show(
                    "설치가 진행 중입니다. 정말 종료하시겠습니까?",
                    "경고",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // 로그 이벤트 구독 해제
            LogManager.Instance.LogAdded -= OnLogAdded;
        }

        private class TaskItem
        {
            public string Name { get; set; } = string.Empty;
            public TaskStatus Status { get; set; }
            public TextBlock IconTextBlock { get; set; } = null!;
            public TextBlock NameTextBlock { get; set; } = null!;
        }
    }

    public enum TaskStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed
    }
}
