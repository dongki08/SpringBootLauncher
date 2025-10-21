using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SpringBootLauncher
{
    /// <summary>
    /// 로그 레벨 정의 (UI 로그용, JAR 로그는 별도)
    /// </summary>
    public enum LogLevel { INFO, SUCCESS, WARNING, ERROR, FILE, RESTART }

    /// <summary>
    /// Spring Boot JAR 파일을 관리하고 실행하는 메인 윈도우
    /// - JAR 프로세스 시작/중지/재시작
    /// - 실시간 로그 모니터링 (ANSI 색상 코드 제거)
    /// - 트레이 아이콘 최소화 지원
    /// - 자동 재시작 (5분 주기 체크)
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 상수 및 정적 필드

        /// <summary>
        /// 애플리케이션 데이터 저장 디렉토리 (%AppData%/SpringBootLauncher)
        /// </summary>
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SpringBootLauncher");

        /// <summary>
        /// 설정 파일 경로 (JAR 경로, 포트, 프로파일 저장)
        /// </summary>
        private static readonly string SettingsPath = Path.Combine(AppDataDir, "settings.txt");

        /// <summary>
        /// UI에 표시할 최대 로그 개수 (메모리 관리)
        /// </summary>
        private const int MAX_LOG_ITEMS = 5000;

        /// <summary>
        /// 로그 버퍼링 타이머 주기 (밀리초)
        /// </summary>
        private const int LOG_UPDATE_INTERVAL_MS = 250;

        /// <summary>
        /// 한 번에 처리할 최대 로그 개수
        /// </summary>
        private const int MAX_LOGS_PER_BATCH = 500;

        /// <summary>
        /// 서버 상태 체크 주기 (분)
        /// </summary>
        private const int SERVER_CHECK_INTERVAL_MINUTES = 5;

        /// <summary>
        /// ANSI 색상 코드 제거용 정규식 (컴파일하여 성능 최적화)
        /// 패턴: ESC[ + 숫자/세미콜론 + 알파벳 (예: \x1B[31m, \x1B[0m)
        /// </summary>
        private static readonly Regex AnsiRegex = new(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

        #endregion

        #region 프로세스 및 타이머 필드

        /// <summary>
        /// Spring Boot JAR 실행 프로세스
        /// </summary>
        private Process? _serverProcess;

        /// <summary>
        /// 서버 상태 체크 타이머 (5분 주기, 중지 시 자동 재시작)
        /// </summary>
        private readonly DispatcherTimer _checkTimer;

        /// <summary>
        /// 가동 시간 업데이트 타이머 (1초 주기)
        /// </summary>
        private readonly DispatcherTimer _uptimeTimer;

        /// <summary>
        /// 로그 버퍼링 타이머 (250ms 주기, UI 업데이트 최적화)
        /// </summary>
        private readonly DispatcherTimer _logUpdateTimer;

        /// <summary>
        /// 서버 시작 시각 (가동 시간 계산용)
        /// </summary>
        private DateTime? _startedAt;

        #endregion

        #region 로그 시스템 필드

        /// <summary>
        /// 로그 버퍼링 큐 (비동기 로그 수신 → UI 일괄 업데이트)
        /// ConcurrentQueue로 스레드 안전성 보장
        /// </summary>
        private readonly ConcurrentQueue<string> _logQueue = new();

        /// <summary>
        /// 사용자가 수동으로 스크롤 중인지 여부 (자동 스크롤 제어)
        /// </summary>
        private bool _isUserScrolling;

        #endregion

        #region 트레이 아이콘 필드

        /// <summary>
        /// 작업 표시줄 트레이 아이콘
        /// </summary>
        private TaskbarIcon? _tray;

        /// <summary>
        /// 트레이 아이콘 리소스 (메모리 누수 방지를 위한 Handle 관리)
        /// </summary>
        private System.Drawing.Icon? _trayIcon;

        #endregion

        #region UI 애니메이션 필드

        /// <summary>
        /// 상태 표시 원형 인디케이터의 색상 브러시 (애니메이션용)
        /// </summary>
        private readonly SolidColorBrush _statusBrush;

        #endregion

        #region 재시작 제어 필드

        /// <summary>
        /// 재시작 중복 실행 방지 플래그
        /// </summary>
        private bool _isRestarting;

        /// <summary>
        /// 마지막 재시작 시도 시각 (10초 내 중복 방지)
        /// </summary>
        private DateTime _lastRestartAttempt = DateTime.MinValue;

        #endregion

        #region 설정 관리

        /// <summary>
        /// 애플리케이션 설정 관리자 (JAR 경로, 포트, 프로파일)
        /// </summary>
        public SettingsManager Settings { get; }

        #endregion

        #region 생성자 및 초기화

        /// <summary>
        /// MainWindow 생성자
        /// 초기화 순서:
        /// 1. UI 컴포넌트 초기화
        /// 2. 앱 데이터 디렉토리 생성
        /// 3. 설정 로드
        /// 4. 타이머 설정
        /// 5. 트레이 아이콘 초기화
        /// 6. 이벤트 핸들러 등록
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // 앱 데이터 디렉토리 생성 (설정 파일 저장용)
            Directory.CreateDirectory(AppDataDir);

            // 윈도우 타이틀바 아이콘 설정
            SetWindowIcon();

            // 설정 파일 로드 (JAR 경로, 포트, 프로파일)
            Settings = new SettingsManager(SettingsPath);
            Settings.Load();
            JarPathTextBox.Text = Settings.JarPath ?? string.Empty;

            // 상태 인디케이터 초기 색상 (빨간색 = 중지됨)
            _statusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")!);
            StatusIndicator.Fill = _statusBrush;

            // 서버 상태 체크 타이머 (5분 주기)
            _checkTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(SERVER_CHECK_INTERVAL_MINUTES) };
            _checkTimer.Tick += CheckTimer_Tick;

            // 가동 시간 업데이트 타이머 (1초 주기)
            _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uptimeTimer.Tick += (_, __) => UpdateUptime();

            // 로그 버퍼링 타이머 (250ms 주기로 큐에서 로그 가져와 UI 업데이트)
            _logUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(LOG_UPDATE_INTERVAL_MS) };
            _logUpdateTimer.Tick += LogUpdateTimer_Tick;
            _logUpdateTimer.Start();

            // 트레이 아이콘 초기화
            InitTray();

            // 버튼 상태 업데이트 (서버 중지 상태로 시작)
            UpdateButtons();

            // 초기 로그 메시지
            AppendLog(LogLevel.INFO, "프로그램 시작");

            // 로그 자동 스크롤 감지 이벤트 등록
            var scrollViewer = GetScrollViewer(LogListBox);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollChanged += LogScrollViewer_ScrollChanged;
            }

            // 윈도우 종료 시 정리 작업 등록
            Closed += (_, __) => CleanupOnExit();

            // 윈도우 크기 변경 시 로그 처리 일시 중지 (전체화면 전환 시 렉 방지)
            SizeChanged += Window_SizeChanged;
        }

        // 크기 변경 디바운스용 타이머
        private DispatcherTimer? _resizeDebounceTimer;

        /// <summary>
        /// 윈도우 크기 변경 이벤트 핸들러
        /// 화면 크기 변경 시 로그 업데이트 및 애니메이션을 일시 중지하여 렉 방지
        /// 디바운스 처리로 크기 변경이 완전히 끝난 후에만 로그 및 애니메이션 재개
        /// </summary>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 크기 변경 중에는 로그 타이머 일시 중지
            if (_logUpdateTimer.IsEnabled)
                _logUpdateTimer.Stop();

            // 상태 인디케이터 애니메이션 일시 중지 (CPU 부하 감소)
            var wasAnimating = StatusIndicator.HasAnimatedProperties;
            if (wasAnimating)
            {
                StatusIndicator.BeginAnimation(OpacityProperty, null);
                StatusIndicator.Opacity = 1.0;
            }

            // 기존 디바운스 타이머 취소
            _resizeDebounceTimer?.Stop();

            // 새 디바운스 타이머 시작 (500ms 동안 크기 변경 없으면 재개)
            _resizeDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _resizeDebounceTimer.Tick += (s, args) =>
            {
                _resizeDebounceTimer?.Stop();

                // 로그 타이머 재개
                if (!_logUpdateTimer.IsEnabled)
                    _logUpdateTimer.Start();

                // 서버 실행 중이고 애니메이션이 실행 중이었으면 재개
                if (wasAnimating && IsRunning)
                {
                    var blinkAnimation = new DoubleAnimation
                    {
                        From = 0.5,
                        To = 1.0,
                        Duration = TimeSpan.FromSeconds(1),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever
                    };
                    StatusIndicator.BeginAnimation(OpacityProperty, blinkAnimation);
                }
            };
            _resizeDebounceTimer.Start();
        }

        #endregion

        #region 트레이 아이콘 관리

        /// <summary>
        /// 트레이 아이콘 초기화
        /// - 아이콘 리소스 로드
        /// - 더블클릭 이벤트 등록 (창 복원)
        /// - 우클릭 컨텍스트 메뉴 생성
        /// </summary>
        private void InitTray()
        {
            // XAML에 정의된 TaskbarIcon 리소스 가져오기
            _tray = (TaskbarIcon)FindResource("TrayIcon");

            // 커스텀 아이콘 로드 (48x48 PNG)
            _trayIcon = LoadIconFromResource("Icons/spring_48.png");
            _tray.Icon = _trayIcon;
            _tray.ToolTipText = "Spring Boot Launcher - 중지됨";

            // 더블클릭 시 창 복원
            _tray.TrayMouseDoubleClick += (_, __) => ShowFromTray();

            // 우클릭 컨텍스트 메뉴 생성
            var ctx = new System.Windows.Controls.ContextMenu();

            // 메뉴 아이템 추가 헬퍼 함수 (로컬 함수)
            void AddMenuItem(string header, RoutedEventHandler handler, bool separatorBefore = false)
            {
                if (separatorBefore)
                    ctx.Items.Add(new System.Windows.Controls.Separator());

                var menuItem = new System.Windows.Controls.MenuItem { Header = header };
                menuItem.Click += handler;
                ctx.Items.Add(menuItem);
            }

            // 컨텍스트 메뉴 구성
            AddMenuItem("열기", (_, __) => ShowFromTray());
            AddMenuItem("서버 시작", (_, __) => SafeFireAndForget(async () => await StartServerAsync(showToast: true)), separatorBefore: true);
            AddMenuItem("서버 중지", (_, __) => SafeFireAndForget(async () => await StopServerAsync(showToast: true)));
            AddMenuItem("서버 재시작", (_, __) => SafeFireAndForget(async () => await RestartServerAsync(showToast: true)));
            AddMenuItem("종료", (_, __) => SafeFireAndForget(async () =>
            {
                await StopServerAsync(showToast: false);
                Application.Current.Shutdown();
            }), separatorBefore: true);

            _tray.ContextMenu = ctx;
        }

        /// <summary>
        /// async 작업을 안전하게 Fire-and-Forget 방식으로 실행
        /// 예외를 로그에 기록하여 프로세스 충돌 방지
        /// </summary>
        /// <param name="task">실행할 비동기 작업</param>
        private async void SafeFireAndForget(Func<Task> task)
        {
            try
            {
                await task();
            }
            catch (Exception ex)
            {
                // 예외를 로그에 기록 (프로세스 충돌 방지)
                AppendLog(LogLevel.ERROR, $"트레이 메뉴 작업 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 윈도우를 트레이로 최소화
        /// 성능 최적화를 위해 백그라운드에서는 UI 업데이트 타이머 및 애니메이션 중지
        /// </summary>
        /// <param name="initial">초기 최소화 여부 (알림 표시 제어)</param>
        public void MinimizeToTray(bool initial = false)
        {
            if (_tray == null)
                return;

            try
            {
                // 윈도우 숨기기
                Hide();

                // 트레이 툴팁 업데이트
                _tray.ToolTipText = IsRunning
                    ? "Spring Boot Launcher - 실행 중"
                    : "Spring Boot Launcher - 중지됨";

                // 성능 최적화: 백그라운드에서는 가동시간 타이머 중지
                if (IsRunning)
                    _uptimeTimer.Stop();

                // 애니메이션 중지 (CPU 사용량 절감)
                StatusIndicator.BeginAnimation(OpacityProperty, null);
                StatusIndicator.Opacity = 1.0;

                // 백그라운드에서는 로그 버퍼링 타이머도 중지 (메모리 절약)
                _logUpdateTimer.Stop();

                // 초기 최소화가 아닐 때만 알림 표시
                if (!initial)
                    ShowTimedBalloonTip("Spring Boot Launcher", "작업 표시줄에서 실행 중입니다.", BalloonIcon.Info);
            }
            catch (ObjectDisposedException)
            {
                // 트레이 아이콘이 이미 해제된 경우 무시
            }
        }

        /// <summary>
        /// 트레이에서 윈도우 복원
        /// 타이머 및 애니메이션 재개, 쌓인 로그 정리
        /// </summary>
        private void ShowFromTray()
        {
            // 백그라운드에서 쌓인 로그 큐 정리 (메모리 및 성능 최적화)
            // 최근 1000개만 유지하고 나머지는 버림
            int logCount = _logQueue.Count;
            if (logCount > 1000)
            {
                // 오래된 로그 제거
                int skipCount = logCount - 1000;
                for (int i = 0; i < skipCount; i++)
                {
                    _logQueue.TryDequeue(out _);
                }

                AppendLog(LogLevel.INFO, $"백그라운드 동안 {skipCount}개의 오래된 로그가 자동 정리되었습니다.");
            }

            // 로그 버퍼링 타이머 재개
            if (!_logUpdateTimer.IsEnabled)
                _logUpdateTimer.Start();

            // 윈도우 표시 및 활성화
            Show();
            WindowState = WindowState.Normal;
            Activate();

            // 서버 실행 중이면 타이머와 애니메이션 재개
            if (IsRunning && _startedAt != null)
            {
                // 가동시간 타이머 재개
                if (!_uptimeTimer.IsEnabled)
                    _uptimeTimer.Start();

                // 상태 인디케이터 깜빡임 애니메이션 재개
                var blinkAnimation = new DoubleAnimation
                {
                    From = 0.5,
                    To = 1.0,
                    Duration = TimeSpan.FromSeconds(1),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                StatusIndicator.BeginAnimation(OpacityProperty, blinkAnimation);
            }
        }

        #endregion

        #region UI 이벤트 핸들러

        /// <summary>
        /// JAR 파일 찾아보기 버튼 클릭 핸들러
        /// </summary>
        private void BrowseJar_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JAR 파일 (*.jar)|*.jar",
                Title = "Spring Boot JAR 선택"
            };

            if (dialog.ShowDialog() == true)
            {
                // JAR 경로 설정 및 저장
                JarPathTextBox.Text = dialog.FileName;
                Settings.JarPath = dialog.FileName;
                Settings.Save();

                AppendLog(LogLevel.FILE, $"JAR 경로 저장: {dialog.FileName}");
            }
        }

        /// <summary>
        /// 설정 버튼 클릭 핸들러
        /// 포트 및 프로파일 설정 다이얼로그 표시
        /// </summary>
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = UniversalDialog.ShowSettings(this, Settings.Port, Settings.Profile);

            if (dialog.IsSaved)
            {
                // 설정 저장
                Settings.Port = dialog.Port;
                Settings.Profile = dialog.Profile;
                Settings.Save();

                AppendLog(LogLevel.INFO,
                    $"설정 저장됨 - 포트: {Settings.Port ?? "(기본값)"}, 프로파일: {Settings.Profile ?? "(없음)"}");
            }
        }

        /// <summary>
        /// 서버 시작 버튼 클릭 핸들러
        /// async void이지만 이벤트 핸들러이므로 예외를 안전하게 처리
        /// </summary>
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await StartServerAsync(showToast: true);
            }
            catch (Exception ex)
            {
                AppendLog(LogLevel.ERROR, $"서버 시작 실패: {ex.Message}");
                UniversalDialog.ShowError(this, "오류", $"서버 시작 중 오류가 발생했습니다.\n\n{ex.Message}");
            }
        }

        /// <summary>
        /// 서버 중지 버튼 클릭 핸들러
        /// async void이지만 이벤트 핸들러이므로 예외를 안전하게 처리
        /// </summary>
        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await StopServerAsync(showToast: true);
            }
            catch (Exception ex)
            {
                AppendLog(LogLevel.ERROR, $"서버 중지 실패: {ex.Message}");
                UniversalDialog.ShowError(this, "오류", $"서버 중지 중 오류가 발생했습니다.\n\n{ex.Message}");
            }
        }

        /// <summary>
        /// 서버 재시작 버튼 클릭 핸들러
        /// async void이지만 이벤트 핸들러이므로 예외를 안전하게 처리
        /// </summary>
        private async void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await RestartServerAsync(showToast: true);
            }
            catch (Exception ex)
            {
                AppendLog(LogLevel.ERROR, $"서버 재시작 실패: {ex.Message}");
                UniversalDialog.ShowError(this, "오류", $"서버 재시작 중 오류가 발생했습니다.\n\n{ex.Message}");
            }
        }

        /// <summary>
        /// 로그 지우기 버튼 클릭 핸들러
        /// 로그 큐와 UI를 모두 초기화
        /// </summary>
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            // 로그 버퍼링 큐 비우기
            while (_logQueue.TryDequeue(out _)) { }

            // UI 로그 리스트 클리어
            LogListBox.Items.Clear();

            AppendLog(LogLevel.INFO, "로그 화면이 지워졌습니다.");
        }

        /// <summary>
        /// 로그 내보내기 버튼 클릭 핸들러
        /// JAR 파일의 로그를 파일로 내보내기
        /// </summary>
        private void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            string? jarPath = JarPathTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(jarPath))
            {
                UniversalDialog.ShowWarning(this, "경로 없음", "JAR 파일 경로를 먼저 선택해주세요.");
                return;
            }

            UniversalDialog.ShowLogExport(this, jarPath);
        }

        /// <summary>
        /// 로그 스크롤 변경 이벤트 핸들러
        /// 자동 스크롤과 수동 스크롤을 구분하여 처리
        /// </summary>
        private void LogScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            var scrollViewer = sender as System.Windows.Controls.ScrollViewer;
            if (scrollViewer == null) return;

            if (e.ExtentHeightChange == 0)
            {
                // 컨텐츠 높이 변화 없음 → 사용자가 수동으로 스크롤함
                // 맨 아래가 아니면 자동 스크롤 비활성화
                _isUserScrolling = scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight - 1;
            }
            else
            {
                // 컨텐츠 높이 변화 있음 → 새 로그 추가됨
                // 수동 스크롤 중이 아니면 자동으로 맨 아래로 스크롤
                if (!_isUserScrolling)
                {
                    scrollViewer.ScrollToEnd();
                }
            }
        }

        /// <summary>
        /// 로그 리스트 키보드 입력 핸들러
        /// Ctrl+C: 선택된 로그 복사 (선택 없으면 전체 복사)
        /// Ctrl+A: 전체 선택
        /// </summary>
        private void LogListBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+C: 로그 복사
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                CopySelectedLogsToClipboard();
                e.Handled = true;
            }
            // Ctrl+A: 전체 선택
            else if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                LogListBox.SelectAll();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 선택된 로그를 클립보드에 복사
        /// 선택이 없으면 전체 로그 복사
        /// </summary>
        private void CopySelectedLogsToClipboard()
        {
            try
            {
                if (LogListBox.SelectedItems.Count == 0)
                {
                    // 선택된 항목이 없으면 전체 로그 복사
                    if (LogListBox.Items.Count == 0)
                    {
                        AppendLog(LogLevel.WARNING, "복사할 로그가 없습니다.");
                        return;
                    }

                    var allLogs = new StringBuilder();
                    foreach (var item in LogListBox.Items)
                    {
                        allLogs.AppendLine(item.ToString());
                    }

                    Clipboard.SetText(allLogs.ToString());
                    AppendLog(LogLevel.INFO, $"전체 로그 {LogListBox.Items.Count}개 복사됨");
                }
                else
                {
                    // 선택된 항목만 복사
                    var selectedLogs = new StringBuilder();
                    foreach (var item in LogListBox.SelectedItems)
                    {
                        selectedLogs.AppendLine(item.ToString());
                    }

                    Clipboard.SetText(selectedLogs.ToString());
                    AppendLog(LogLevel.INFO, $"선택된 로그 {LogListBox.SelectedItems.Count}개 복사됨");
                }
            }
            catch (Exception ex)
            {
                AppendLog(LogLevel.ERROR, $"클립보드 복사 실패: {ex.Message}");
            }
        }

        #endregion

        #region Server Control
        private bool IsRunning => _serverProcess != null && !_serverProcess.HasExited;

        public async Task StartServerAsync(bool showToast)
        {
            // 시작 시 로그 클리어
            while (_logQueue.TryDequeue(out _)) { } // 큐 비우기
            LogListBox.Items.Clear();
            _isUserScrolling = false; // 자동 스크롤 재활성화

            try
            {
                string? jar = JarPathTextBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(jar) || !File.Exists(jar))
                {
                    AppendLog(LogLevel.ERROR, "JAR 파일이 존재하지 않습니다. (E-001)");
                    UniversalDialog.ShowWarning(this, "경고", "JAR 파일을 선택하세요.");
                    return;
                }

                if (IsRunning)
                {
                    AppendLog(LogLevel.WARNING, "이미 실행 중입니다. (E-004)");
                    UniversalDialog.ShowInfo(this, "알림", "이미 실행 중입니다.");
                    return;
                }

                // Java 설치 여부 체크 (캐싱으로 최적화)
                if (!await CheckJavaInstalled())
                {
                    AppendLog(LogLevel.ERROR, "Java 미설치 또는 PATH 미설정 (E-002)");
                    UniversalDialog.ShowError(this, "오류", "Java가 설치되어 있지 않거나 PATH가 설정되지 않았습니다.");
                    return;
                }

                // 인자 구성: 포트, 프로파일
                string args = $"-jar \"{jar}\"";

                // 포트 설정
                if (!string.IsNullOrEmpty(Settings.Port))
                {
                    args += $" --server.port={Settings.Port}";
                    AppendLog(LogLevel.INFO, $"포트 설정: {Settings.Port}");
                }

                // 프로파일 설정
                if (!string.IsNullOrEmpty(Settings.Profile))
                {
                    args += $" --spring.profiles.active={Settings.Profile}";
                    AppendLog(LogLevel.INFO, $"프로파일 적용: {Settings.Profile}");
                }

                var psi = new ProcessStartInfo("java", args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = Path.GetDirectoryName(jar) ?? Environment.CurrentDirectory
                };

                _serverProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _serverProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // ANSI 색상 코드 제거 후 출력
                        var cleanLog = AnsiRegex.Replace(e.Data, "");
                        _logQueue.Enqueue(cleanLog);
                    }
                };
                _serverProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // ANSI 색상 코드 제거 후 출력
                        var cleanLog = AnsiRegex.Replace(e.Data, "");
                        _logQueue.Enqueue(cleanLog);
                    }
                };
                _serverProcess.Exited += (_, __) =>
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        AppendLog(LogLevel.WARNING, "프로세스 종료 감지");
                        OnStopped();
                    }, DispatcherPriority.Normal);
                };

                if (!_serverProcess.Start())
                    throw new Exception("프로세스 시작 실패 (E-003)");

                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();

                _startedAt = DateTime.Now;
                _uptimeTimer.Start();
                _checkTimer.Start();

                OnStarted();

                if (showToast)
                    ShowTimedBalloonTip("Spring Boot Launcher", "서버가 시작되었습니다.", BalloonIcon.Info);

                AppendLog(LogLevel.SUCCESS, "✅ 서버 시작됨");
            }
            catch (Exception ex)
            {
                AppendLog(LogLevel.ERROR, $"프로세스 시작 실패: {ex.Message}");
                UniversalDialog.ShowError(this, "오류", $"프로세스 시작 실패: {ex.Message}");
            }
        }

        public async Task StopServerAsync(bool showToast)
        {
            if (!IsRunning)
            {
                AppendLog(LogLevel.INFO, "이미 중지됨");
                return;
            }

            // 사용자 확인 (트레이에서 호출 시에만)
            if (showToast)
            {
                bool confirmed = UniversalDialog.ShowQuestion(this, "서버 중지 확인", "서버를 중지하시겠습니까?");
                if (!confirmed)
                {
                    AppendLog(LogLevel.INFO, "서버 중지 취소됨");
                    return;
                }
            }

            try
            {
                var p = _serverProcess!;
                try
                {
                    // 강제 종료
                    p.Kill(entireProcessTree: true);
                }
                catch { /* 무시 */ }

                // 최대 5초 대기
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await p.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // 여전히 종료 안 됨
                }
                finally
                {
                    cts.Dispose();
                }

                AppendLog(LogLevel.INFO, "서버 중지 시도 완료");

                // Process 리소스 해제
                try
                {
                    p.Dispose();
                }
                catch { /* 무시 */ }

                _serverProcess = null;

                // 성공적으로 중지됨
                OnStopped();
                if (showToast)
                    ShowTimedBalloonTip("Spring Boot Launcher", "서버가 중지되었습니다.", BalloonIcon.Info);
            }
            catch (Exception ex)
            {
                AppendLog(LogLevel.ERROR, $"서버 중지 실패: {ex.Message}");
            }
        }

        public async Task RestartServerAsync(bool showToast)
        {
            // 재시작 중복 방지
            if (_isRestarting)
            {
                AppendLog(LogLevel.WARNING, "이미 재시작 중입니다.");
                return;
            }

            _isRestarting = true;

            try
            {
                // 재시작 시 로그 클리어
                while (_logQueue.TryDequeue(out _)) { }
                LogListBox.Items.Clear();
                _isUserScrolling = false; // 자동 스크롤 재활성화

                AppendLog(LogLevel.RESTART, "재시작 수행");
                await StopServerAsync(showToast: false);
                await Task.Delay(2000); // 2초 대기
                await StartServerAsync(showToast);
            }
            finally
            {
                _isRestarting = false;
            }
        }

        private void OnStarted()
        {
            SetStatusRunning(true);
            UpdateButtons();
        }

        private void OnStopped()
        {
            _uptimeTimer.Stop();
            _checkTimer.Stop();
            _startedAt = null;
            SetStatusRunning(false);
            UpdateButtons();
            UptimeTextBlock.Text = "00:00:00";
        }
        #endregion

        #region Timers
        private void CheckTimer_Tick(object? sender, EventArgs e)
        {
            StatusBarText.Text = $"체크 주기: 5분 | 마지막 체크: {DateTime.Now:HH:mm:ss}";

            try
            {
                // 5분마다 로그 정리 (메모리 관리)
                if (LogListBox.Items.Count > 2000)
                {
                    LogListBox.Items.Clear();
                    AppendLog(LogLevel.INFO, "로그 자동 정리됨 (5분 주기)");
                }

                if (IsRunning)
                {
                    AppendLog(LogLevel.SUCCESS, "✓ 서버 정상 작동 중");
                    return;
                }

                // 재시작 중복 방지 (10초 이내 재시도 금지)
                if ((DateTime.Now - _lastRestartAttempt).TotalSeconds < 10)
                {
                    return;
                }

                _lastRestartAttempt = DateTime.Now;

                AppendLog(LogLevel.WARNING, "서버 중지 감지 → 자동 재시작 시도");
                ShowTimedBalloonTip("Spring Boot Launcher", "서버 중지 감지, 재시작합니다.", BalloonIcon.Warning);

                // 비동기 재시작
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    await Dispatcher.InvokeAsync(async () => await StartServerAsync(showToast: true));
                });
            }
            catch (Exception ex)
            {
                AppendLog(LogLevel.ERROR, $"체크 실패: {ex.Message}");
            }
        }

        private void UpdateUptime()
        {
            if (_startedAt == null) return;
            var span = DateTime.Now - _startedAt.Value;

            // 100시간 미만: 00:00:00 형식
            if (span.TotalHours < 100)
            {
                UptimeTextBlock.Text = $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
            }
            // 100시간 이상: N일 HH:MM:SS 형식
            else
            {
                int days = (int)span.TotalDays;
                int hours = span.Hours;
                UptimeTextBlock.Text = $"{days}일 {hours:00}:{span.Minutes:00}:{span.Seconds:00}";
            }
        }
        #endregion

        #region UI/State
        private void UpdateButtons()
        {
            if (IsRunning)
            {
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                RestartButton.IsEnabled = true;
                BrowseButton.IsEnabled = false;
                SettingsButton.IsEnabled = false;
                JarPathTextBox.IsReadOnly = true;
            }
            else
            {
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                RestartButton.IsEnabled = false;
                BrowseButton.IsEnabled = true;
                SettingsButton.IsEnabled = true;
                JarPathTextBox.IsReadOnly = false;
            }
        }

        private void SetStatusRunning(bool running)
        {
            StatusTextBlock.Text = running ? "실행 중" : "중지됨";
            if (_tray != null)
                _tray.ToolTipText = running ? "Spring Boot Launcher - 실행 중" : "Spring Boot Launcher - 중지됨";

            var from = ((SolidColorBrush)StatusIndicator.Fill).Color;
            var to = running
                ? (Color)ColorConverter.ConvertFromString("#0D9668")!
                : (Color)ColorConverter.ConvertFromString("#DC2626")!;

            var anim = new ColorAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(400)
            };
            _statusBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);

            // 실행 중일 때 살짝 깜빡임 효과
            if (running)
            {
                var blink = new DoubleAnimation
                {
                    From = 0.5,
                    To = 1.0,
                    Duration = TimeSpan.FromSeconds(1),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                StatusIndicator.BeginAnimation(OpacityProperty, blink);
            }
            else
            {
                StatusIndicator.BeginAnimation(OpacityProperty, null);
                StatusIndicator.Opacity = 1.0;
            }
        }
        #endregion

        #region Logging
        // 로그 버퍼링 타이머 - 250ms마다 큐에서 로그를 가져와 UI에 일괄 추가
        private void LogUpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_logQueue.IsEmpty) return;

            var itemsToAdd = new System.Collections.Generic.List<string>();
            while (_logQueue.TryDequeue(out var logItem))
            {
                itemsToAdd.Add(logItem);
                if (itemsToAdd.Count >= 500) break; // 한 번에 최대 500개로 증가
            }

            if (itemsToAdd.Count == 0) return;

            // UI 로그 아이템 수 관리 - 최대 개수 초과 시 오래된 로그 제거
            if (LogListBox.Items.Count + itemsToAdd.Count > MAX_LOG_ITEMS)
            {
                int removeCount = (LogListBox.Items.Count + itemsToAdd.Count) - MAX_LOG_ITEMS + 500;
                for (int i = 0; i < removeCount && LogListBox.Items.Count > 0; i++)
                {
                    LogListBox.Items.RemoveAt(0);
                }

                // 정리 알림 추가
                if (LogListBox.Items.Count == 0 || !LogListBox.Items[^1].ToString()!.Contains("로그 자동 정리"))
                {
                    LogListBox.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ℹ️ INFO: 오래된 로그 {removeCount}개 자동 정리됨");
                }
            }

            // 로그 추가 전 스크롤 위치 체크
            var scrollViewer = GetScrollViewer(LogListBox);
            bool wasAtBottom = false;
            if (scrollViewer != null)
            {
                // 맨 아래에서 50픽셀 이내면 자동 스크롤 (여유 증가)
                wasAtBottom = !_isUserScrolling && (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 50);
            }

            // 일괄 추가
            foreach (var item in itemsToAdd)
            {
                LogListBox.Items.Add(item);
            }

            // 스마트 자동 스크롤 - 사용자가 맨 아래에 있었고 수동 스크롤 중이 아닐 때만
            if (wasAtBottom && LogListBox.Items.Count > 0 && !_isUserScrolling)
            {
                // Dispatcher를 사용하여 레이아웃 업데이트 후 스크롤
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        if (scrollViewer != null)
                        {
                            scrollViewer.ScrollToEnd();
                        }
                    }
                    catch { /* 무시 */ }
                }, DispatcherPriority.Loaded);
            }
        }

        public void AppendLog(LogLevel level, string message)
        {
            string prefix = level switch
            {
                LogLevel.INFO => "ℹ️ INFO",
                LogLevel.SUCCESS => "✅ SUCCESS",
                LogLevel.WARNING => "⚠️ WARNING",
                LogLevel.ERROR => "❌ ERROR",
                LogLevel.FILE => "📁 FILE",
                LogLevel.RESTART => "🔄 RESTART",
                _ => "INFO"
            };
            // 큐에 추가
            _logQueue.Enqueue($"[{DateTime.Now:HH:mm:ss}] {prefix}: {message}");
        }

        // GetScrollViewer 헬퍼 메서드 (ListBox 내부의 ScrollViewer 가져오기)
        private System.Windows.Controls.ScrollViewer? GetScrollViewer(System.Windows.DependencyObject element)
        {
            if (element is System.Windows.Controls.ScrollViewer scrollViewer)
                return scrollViewer;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(element, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }

            return null;
        }

        #endregion

        #region Icon Loading
        private void SetWindowIcon()
        {
            try
            {
                // 32x32 아이콘 사용 (윈도우 타이틀바에 최적)
                var uri = new Uri("pack://application:,,,/Icons/spring_56.png");
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;

                // 선명한 렌더링 (안티앨리어싱 최소화)
                System.Windows.Media.RenderOptions.SetBitmapScalingMode(bitmap, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
                bitmap.EndInit();

                this.Icon = bitmap;
            }
            catch
            {
                // 아이콘 로드 실패 시 무시
            }
        }

        private System.Drawing.Icon LoadIconFromResource(string resourcePath)
        {
            try
            {
                // 16x16 전용 PNG 사용 (리샘플링 없이 직접 사용)
                var uri = new Uri("pack://application:,,,/Icons/spring_48.png");
                var streamInfo = Application.GetResourceStream(uri);

                if (streamInfo != null)
                {
                    using var bitmap = new System.Drawing.Bitmap(streamInfo.Stream);
                    var handle = bitmap.GetHicon();
                    var icon = System.Drawing.Icon.FromHandle(handle);

                    return icon;
                }
            }
            catch (Exception ex)
            {
                AppendLog(LogLevel.WARNING, $"아이콘 로드 실패: {ex.Message}");
            }

            // 실패 시 기본 아이콘 반환
            return System.Drawing.SystemIcons.Application;
        }
        #endregion

        #region Helpers
        // 현재 표시 중인 알림 창
        private NotificationWindow? _currentNotification;

        // 3초 후 자동으로 닫히는 커스텀 알림
        private void ShowTimedBalloonTip(string title, string message, BalloonIcon icon)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 기존 알림이 있으면 닫기
                    if (_currentNotification != null)
                    {
                        _currentNotification.Close();
                        _currentNotification = null;
                    }

                    // 새 알림 창 생성 및 표시
                    _currentNotification = new NotificationWindow();
                    _currentNotification.Closed += (s, e) => _currentNotification = null;
                    _currentNotification.Show(title, message, icon, 3000);
                }
                catch (Exception ex)
                {
                    // 알림 표시 실패 시 로그만 남김
                    AppendLog(LogLevel.WARNING, $"알림 표시 실패: {ex.Message}");
                }
            });
        }
        
        // Java 설치 확인 (캐싱)
        private static bool? _javaInstalled = null;
        private async Task<bool> CheckJavaInstalled()
        {
            // 캐시된 결과 사용
            if (_javaInstalled.HasValue)
                return _javaInstalled.Value;

            try
            {
                var psi = new ProcessStartInfo("java", "-version")
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null) return false;

                await p.WaitForExitAsync();
                _javaInstalled = p.ExitCode == 0;
                return _javaInstalled.Value;
            }
            catch
            {
                _javaInstalled = false;
                return false;
            }
        }
        #endregion

        #region Cleanup/Exit
        public void CleanupOnExit()
        {
            try
            {
                _checkTimer.Stop();
                _uptimeTimer.Stop();
                _logUpdateTimer.Stop(); // 로그 버퍼링 타이머 중지

                if (IsRunning)
                {
                    _serverProcess?.Kill(entireProcessTree: true);
                    _serverProcess?.Dispose();
                }

                // Icon Handle 해제
                _trayIcon?.Dispose();

                if (_tray != null)
                {
                    _tray.Dispose();
                    _tray = null;
                }
            }
            catch { /* 무시 */ }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_tray != null)
            {
                e.Cancel = true;
                MinimizeToTray();
            }
            else
            {
                base.OnClosing(e);
            }
        }
        #endregion
    }

    #region SettingsManager
    public sealed class SettingsManager
    {
        private readonly string _path;
        private static readonly StringBuilder _sharedBuilder = new StringBuilder(256);

        public string? JarPath { get; set; }
        public string? Port { get; set; }
        public string? Profile { get; set; }

        public SettingsManager(string path) => _path = path;

        public bool HasValidJarPath() => !string.IsNullOrWhiteSpace(JarPath) && File.Exists(JarPath);

        public void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                var lines = File.ReadAllLines(_path, Encoding.UTF8);
                if (lines.Length >= 1) JarPath = lines[0].Trim();

                // 기존 파일 형식 호환: AUTO_START 라인 건너뛰기
                if (lines.Length >= 2)
                {
                    var line1 = lines[1].Trim();
                    if (!line1.StartsWith("AUTO_START=", StringComparison.OrdinalIgnoreCase))
                    {
                        Profile = line1;
                    }
                }

                if (lines.Length >= 3)
                {
                    var line2 = lines[2].Trim();
                    // AUTO_START 다음이 Profile인 경우
                    if (lines.Length >= 2 && lines[1].Trim().StartsWith("AUTO_START=", StringComparison.OrdinalIgnoreCase))
                    {
                        Profile = line2;
                    }
                    else
                    {
                        Port = line2;
                    }
                }

                if (lines.Length >= 4)
                {
                    Port = lines[3].Trim();
                }
            }
            catch
            {
                // E-005: 설정 로드 실패 → 무시 + 기본값 사용
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

                lock (_sharedBuilder)
                {
                    _sharedBuilder.Clear();
                    _sharedBuilder.AppendLine(JarPath ?? string.Empty);
                    _sharedBuilder.AppendLine(Profile ?? string.Empty);
                    _sharedBuilder.AppendLine(Port ?? string.Empty);
                    File.WriteAllText(_path, _sharedBuilder.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // 저장 실패는 조용히 무시
            }
        }
    }
    #endregion
}
