using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;

namespace SpringBootLauncher
{
    /// <summary>
    /// 트레이 아이콘 알림 윈도우 (Toast 스타일)
    /// - 화면 우측 하단에 표시
    /// - 페이드 인/아웃 애니메이션
    /// - 자동 닫기 (기본 3초)
    /// </summary>
    public partial class NotificationWindow : Window
    {
        /// <summary>
        /// 자동 닫기 타이머
        /// </summary>
        private DispatcherTimer? _closeTimer;

        /// <summary>
        /// NotificationWindow 생성자
        /// </summary>
        public NotificationWindow()
        {
            InitializeComponent();

            // 화면 우측 하단에 위치 (작업표시줄 위)
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - Width - 10;
            Top = workingArea.Bottom - Height - 10;

            // 클릭하면 즉시 닫기
            MouseDown += (s, e) => CloseWithAnimation();
        }

        /// <summary>
        /// 알림 표시 (페이드 인 효과와 함께)
        /// </summary>
        /// <param name="title">알림 제목</param>
        /// <param name="message">알림 메시지</param>
        /// <param name="icon">알림 아이콘 타입</param>
        /// <param name="durationMs">표시 지속 시간 (밀리초, 기본 3초)</param>
        public void Show(string title, string message, BalloonIcon icon, int durationMs = 3000)
        {
            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;

            // 아이콘 설정 (현재는 모두 같은 Spring 아이콘 사용)
            try
            {
                string iconPath = icon switch
                {
                    BalloonIcon.Info => "pack://application:,,,/Icons/spring_48.png",
                    BalloonIcon.Warning => "pack://application:,,,/Icons/spring_48.png",
                    BalloonIcon.Error => "pack://application:,,,/Icons/spring_48.png",
                    _ => "pack://application:,,,/Icons/spring_48.png"
                };

                IconImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
            }
            catch
            {
                // 아이콘 로드 실패 시 숨김 처리
                IconImage.Visibility = Visibility.Collapsed;
            }

            // 페이드 인 애니메이션 (0 → 1)
            Opacity = 0;
            base.Show();

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            BeginAnimation(OpacityProperty, fadeIn);

            // 자동 닫기 타이머 시작
            _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
            _closeTimer.Tick += (s, e) =>
            {
                _closeTimer?.Stop();
                CloseWithAnimation();
            };
            _closeTimer.Start();
        }

        /// <summary>
        /// 닫기 버튼 클릭 핸들러
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWithAnimation();
        }

        /// <summary>
        /// 페이드 아웃 애니메이션과 함께 창 닫기
        /// </summary>
        private void CloseWithAnimation()
        {
            // 타이머 정지
            _closeTimer?.Stop();

            // 페이드 아웃 애니메이션 (1 → 0)
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, e) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        }

        /// <summary>
        /// 윈도우 종료 시 리소스 정리
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _closeTimer?.Stop();
            _closeTimer = null;
            base.OnClosed(e);
        }
    }
}
