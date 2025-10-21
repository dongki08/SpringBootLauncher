using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SpringBootLauncher
{
    /// <summary>
    /// 다목적 다이얼로그 윈도우
    /// - 설정 다이얼로그 (포트, 프로파일)
    /// - 로그 내보내기 다이얼로그
    /// - 경고/정보/에러 메시지 다이얼로그
    /// - 확인/취소 다이얼로그
    /// </summary>
    public partial class UniversalDialog : Window
    {
        #region 공용 프로퍼티

        /// <summary>
        /// 사용자가 저장 버튼을 클릭했는지 여부
        /// </summary>
        public bool IsSaved { get; private set; }

        /// <summary>
        /// 설정된 포트 번호
        /// </summary>
        public string? Port { get; private set; }

        /// <summary>
        /// 설정된 Spring 프로파일
        /// </summary>
        public string? Profile { get; private set; }

        #endregion

        #region 내부 필드

        /// <summary>
        /// 포트 번호 입력 텍스트박스 (설정 다이얼로그용)
        /// </summary>
        private TextBox _portTextBox = null!;

        /// <summary>
        /// 프로파일 입력 텍스트박스 (설정 다이얼로그용)
        /// </summary>
        private TextBox _profileTextBox = null!;

        /// <summary>
        /// 시작 날짜 선택기 (로그 내보내기용)
        /// </summary>
        private DatePicker _startDatePicker = null!;

        /// <summary>
        /// 종료 날짜 선택기 (로그 내보내기용)
        /// </summary>
        private DatePicker _endDatePicker = null!;

        /// <summary>
        /// 저장 경로 텍스트박스 (로그 내보내기용)
        /// </summary>
        private TextBox _savePathTextBox = null!;

        /// <summary>
        /// JAR 파일 경로 (로그 내보내기용)
        /// </summary>
        private readonly string? _jarPath;

        #endregion

        #region 생성자 및 기본 이벤트

        /// <summary>
        /// UniversalDialog 생성자 (private - 정적 팩토리 메서드로만 생성)
        /// </summary>
        private UniversalDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 윈도우 드래그 이동 (타이틀바 없는 윈도우용)
        /// </summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// 닫기 버튼 클릭 (다이얼로그 취소)
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 버튼1 클릭 핸들러 (각 다이얼로그에서 개별 로직 처리)
        /// </summary>
        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            // 정적 팩토리 메서드에서 개별 로직 설정
        }

        /// <summary>
        /// 버튼2 클릭 핸들러 (일반적으로 취소)
        /// </summary>
        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        public static void ShowMessage(Window? owner, string title, string message, string icon)
        {
            var dialog = new UniversalDialog
            {
                Owner = owner,
            };
            dialog.TitleTextBlock.Text = title;
            dialog.IconTextBlock.Text = icon;
            
            var textBlock = new TextBlock 
            { 
                Text = message, 
                TextWrapping = TextWrapping.Wrap, 
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                LineHeight = 22,
                Foreground = (SolidColorBrush)dialog.FindResource("BodyText")
            };
            dialog.MainContent.Content = textBlock;

            dialog.Button1.Content = "확인";
            dialog.Button1.Click += (s, ev) => { dialog.DialogResult = true; dialog.Close(); };
            dialog.Button2.Visibility = Visibility.Collapsed;
            dialog.ShowDialog();
        }

        public static void ShowInfo(Window? owner, string title, string message) => ShowMessage(owner, title, message, "ℹ️");
        public static void ShowWarning(Window? owner, string title, string message) => ShowMessage(owner, title, message, "⚠️");
        public static void ShowError(Window? owner, string title, string message) => ShowMessage(owner, title, message, "❌");

        public static bool ShowQuestion(Window? owner, string title, string message)
        {
            var dialog = new UniversalDialog
            {
                Owner = owner,
            };
            dialog.TitleTextBlock.Text = title;
            dialog.IconTextBlock.Text = "❓";
            
            var textBlock = new TextBlock 
            { 
                Text = message, 
                TextWrapping = TextWrapping.Wrap, 
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                LineHeight = 22,
                Foreground = (SolidColorBrush)dialog.FindResource("BodyText")
            };
            dialog.MainContent.Content = textBlock;

            bool result = false;
            dialog.Button1.Content = "예";
            dialog.Button1.Click += (s, ev) => { result = true; dialog.Close(); };
            dialog.Button2.Content = "아니오";
            dialog.Button2.Click += (s, ev) => { result = false; dialog.Close(); };

            dialog.ShowDialog();
            return result;
        }


        public static UniversalDialog ShowSettings(Window? owner, string? currentPort, string? currentProfile)
        {
            var dialog = new UniversalDialog
            {
                Owner = owner,
            };
            dialog.TitleTextBlock.Text = "⚙️ 서버 설정";
            dialog.IconTextBlock.Visibility = Visibility.Collapsed;

            var panel = new StackPanel { Margin = new Thickness(0) };
            
            // 포트 번호 섹션
            panel.Children.Add(new TextBlock 
            { 
                Text = "포트 번호", 
                FontWeight = FontWeights.SemiBold, 
                FontSize = 14,
                Foreground = (SolidColorBrush)dialog.FindResource("HeaderText"),
                Margin = new Thickness(0, 0, 0, 8) 
            });
            
            dialog._portTextBox = new TextBox
            {
                Text = currentPort ?? string.Empty,
                ToolTip = "서버 포트 번호 (비워두면 JAR 파일 기본값 사용)",
                Style = (Style)dialog.FindResource("ModernTextBox")
            };
            panel.Children.Add(dialog._portTextBox);
            
            panel.Children.Add(new TextBlock 
            { 
                Text = "💡 비워두면 JAR 파일의 기본 포트를 사용합니다", 
                Foreground = (SolidColorBrush)dialog.FindResource("MutedText"),
                FontSize = 12, 
                Margin = new Thickness(0, 6, 0, 20) 
            });

            // 프로파일 섹션
            panel.Children.Add(new TextBlock 
            { 
                Text = "프로파일 (Profile)", 
                FontWeight = FontWeights.SemiBold, 
                FontSize = 14,
                Foreground = (SolidColorBrush)dialog.FindResource("HeaderText"),
                Margin = new Thickness(0, 0, 0, 8) 
            });
            
            dialog._profileTextBox = new TextBox
            {
                Text = currentProfile ?? string.Empty,
                ToolTip = "Spring 프로파일 (예: dev, prod)",
                Style = (Style)dialog.FindResource("ModernTextBox")
            };
            panel.Children.Add(dialog._profileTextBox);
            
            panel.Children.Add(new TextBlock 
            { 
                Text = "💡 비워두면 프로파일 없이 실행됩니다", 
                Foreground = (SolidColorBrush)dialog.FindResource("MutedText"),
                FontSize = 12, 
                Margin = new Thickness(0, 6, 0, 0) 
            });

            dialog.MainContent.Content = panel;

            dialog.Button1.Content = "💾 저장";
            dialog.Button1.Click += dialog.SaveSettings_Click;
            dialog.Button2.Content = "취소";

            dialog.ShowDialog();
            return dialog;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            Port = _portTextBox.Text;
            Profile = _profileTextBox.Text;
            IsSaved = true;
            DialogResult = true;
            Close();
        }

        public static void ShowLogExport(Window? owner, string jarPath)
        {
            var dialog = new UniversalDialog(jarPath)
            {
                Owner = owner,
                Width = 580
            };
            dialog.TitleTextBlock.Text = "📤 로그 Export";
            dialog.IconTextBlock.Visibility = Visibility.Collapsed;

            var panel = new StackPanel { Margin = new Thickness(0) };
            
            // 시작 날짜
            panel.Children.Add(new TextBlock 
            { 
                Text = "📅 시작 날짜", 
                FontWeight = FontWeights.SemiBold, 
                FontSize = 14,
                Foreground = (SolidColorBrush)dialog.FindResource("HeaderText"),
                Margin = new Thickness(0, 0, 0, 8) 
            });
            dialog._startDatePicker = new DatePicker 
            { 
                SelectedDate = DateTime.Today,
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 20)
            };
            panel.Children.Add(dialog._startDatePicker);

            // 종료 날짜
            panel.Children.Add(new TextBlock 
            { 
                Text = "📅 종료 날짜", 
                FontWeight = FontWeights.SemiBold, 
                FontSize = 14,
                Foreground = (SolidColorBrush)dialog.FindResource("HeaderText"),
                Margin = new Thickness(0, 0, 0, 8) 
            });
            dialog._endDatePicker = new DatePicker 
            { 
                SelectedDate = DateTime.Today,
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 20)
            };
            panel.Children.Add(dialog._endDatePicker);

            // 저장 경로
            panel.Children.Add(new TextBlock 
            { 
                Text = "💾 저장 경로", 
                FontWeight = FontWeights.SemiBold, 
                FontSize = 14,
                Foreground = (SolidColorBrush)dialog.FindResource("HeaderText"),
                Margin = new Thickness(0, 0, 0, 8) 
            });
            
            var pathGrid = new Grid();
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            dialog._savePathTextBox = new TextBox 
            { 
                IsReadOnly = true,
                Style = (Style)dialog.FindResource("ModernTextBox"),
                Background = new SolidColorBrush(Color.FromRgb(249, 250, 251))
            };
            Grid.SetColumn(dialog._savePathTextBox, 0);
            
            var browseButton = new Button 
            { 
                Content = "📁 찾아보기", 
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(16, 10, 16, 10),
                Style = (Style)dialog.FindResource("SecondaryButtonStyle")
            };
            browseButton.Click += dialog.BrowseSavePath_Click;
            Grid.SetColumn(browseButton, 1);
            
            pathGrid.Children.Add(dialog._savePathTextBox);
            pathGrid.Children.Add(browseButton);
            panel.Children.Add(pathGrid);

            dialog.MainContent.Content = panel;

            dialog.Button1.Content = "📤 Export";
            dialog.Button1.Click += dialog.ExportButton_Click;
            dialog.Button2.Content = "취소";

            dialog.ShowDialog();
        }
        
        private UniversalDialog(string jarPath) : this()
        {
            _jarPath = jarPath;
        }


        private void BrowseSavePath_Click(object sender, RoutedEventArgs e)
        {
            // JAR 파일명에서 프로젝트명 추출 (예: SoulbrainACS2-0.0.1.jar -> SoulbrainACS2)
            string projectName = "Project";
            if (!string.IsNullOrEmpty(_jarPath))
            {
                var fileName = Path.GetFileNameWithoutExtension(_jarPath);
                // 버전 정보 제거 (- 이후 제거)
                var dashIndex = fileName.IndexOf('-');
                projectName = dashIndex > 0 ? fileName.Substring(0, dashIndex) : fileName;
            }
            
            // 기본 저장 경로: C:\ACS\log\{프로젝트명}
            string defaultDirectory = Path.Combine(@"C:\ACS\log\", projectName);
            Directory.CreateDirectory(defaultDirectory);
            
            // 파일명 형식: ACS_Log_From_날짜_To_날짜.txt
            string defaultFileName = "ACS_Log_From_날짜선택_To_날짜선택.txt";
            if (_startDatePicker.SelectedDate.HasValue && _endDatePicker.SelectedDate.HasValue)
            {
                defaultFileName = $"ACS_Log_From_{_startDatePicker.SelectedDate.Value:yyyy_MM_dd}_To_{_endDatePicker.SelectedDate.Value:yyyy_MM_dd}.txt";
            }
            
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
                FileName = defaultFileName,
                InitialDirectory = defaultDirectory
            };
            
            if (saveFileDialog.ShowDialog() == true)
            {
                _savePathTextBox.Text = saveFileDialog.FileName;
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            // 유효성 검사
            if (_startDatePicker.SelectedDate == null || _endDatePicker.SelectedDate == null)
            {
                ShowWarning(this, "날짜 오류", "시작 날짜와 종료 날짜를 모두 선택해주세요.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_savePathTextBox.Text))
            {
                ShowWarning(this, "경로 오류", "저장할 파일 경로를 선택해주세요.");
                return;
            }

            var startDate = _startDatePicker.SelectedDate.Value;
            var endDate = _endDatePicker.SelectedDate.Value;
            
            // 프로젝트명 추출
            string projectName = ExtractProjectName(_jarPath);
            
            // 로그 폴더: C:\ACS\log\{프로젝트명}
            var logDirectory = Path.Combine(@"C:\ACS\log", projectName);

            if (!Directory.Exists(logDirectory))
            {
                ShowError(this, "폴더 없음", $"로그 폴더를 찾을 수 없습니다.\n경로: {logDirectory}");
                return;
            }

            try
            {
                // 로그 파일 검색
                var logFiles = FindLogFiles(logDirectory, startDate, endDate);

                if (logFiles.Count == 0)
                {
                    ShowInfo(this, "결과 없음", "선택한 기간에 해당하는 로그 파일이 없습니다.");
                    return;
                }

                // Export 실행
                ExportLogs(logFiles, _savePathTextBox.Text, startDate, endDate);

                ShowInfo(this, "성공", $"로그를 성공적으로 Export했습니다.\n\n파일 개수: {logFiles.Count}개\n저장 위치: {_savePathTextBox.Text}");
                Close();
            }
            catch (Exception ex)
            {
                ShowError(this, "Export 실패", $"로그를 Export하는 중 오류가 발생했습니다:\n{ex.Message}");
            }
        }

        // 프로젝트명 추출 (SoulbrainACS2-0.0.1 -> SoulbrainACS2)
        private static string ExtractProjectName(string? jarPath)
        {
            if (string.IsNullOrEmpty(jarPath))
                return "Project";

            var fileName = Path.GetFileNameWithoutExtension(jarPath);
            var dashIndex = fileName.IndexOf('-');
            return dashIndex > 0 ? fileName.Substring(0, dashIndex) : fileName;
        }

        // 로그 파일 검색 (ACS_Log_2025_10_16.txt 형식)
        private static List<string> FindLogFiles(string logDirectory, DateTime startDate, DateTime endDate)
        {
            var logFiles = new List<string>();

            // 모든 텍스트/로그 파일 가져오기
            var allFiles = Directory.GetFiles(logDirectory, "*.txt")
                .Concat(Directory.GetFiles(logDirectory, "*.log"))
                .ToList();

            foreach (var file in allFiles)
            {
                var fileName = Path.GetFileName(file);

                // ACS_Log_YYYY_MM_DD.txt 형식 파싱
                if (fileName.StartsWith("ACS_Log_"))
                {
                    var parts = fileName.Replace("ACS_Log_", "").Split(new[] { '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (parts.Length >= 3 &&
                        int.TryParse(parts[0], out int year) &&
                        int.TryParse(parts[1], out int month) &&
                        int.TryParse(parts[2], out int day))
                    {
                        try
                        {
                            var date = new DateTime(year, month, day);
                            if (date >= startDate && date <= endDate)
                            {
                                logFiles.Add(file);
                            }
                        }
                        catch { }
                    }
                }
            }

            return logFiles.OrderBy(f => f).ToList();
        }

        // 로그 Export 실행
        private static void ExportLogs(List<string> logFiles, string outputPath, DateTime startDate, DateTime endDate)
        {
            // UTF-8 BOM으로 저장 (Windows 한글 호환)
            var encoding = new UTF8Encoding(true);

            using (var writer = new StreamWriter(outputPath, false, encoding))
            {
                // 헤더
                WriteHeader(writer, startDate, endDate, logFiles);

                // 로그 내용 병합
                foreach (var logFile in logFiles)
                {
                    WriteLogFile(writer, logFile);
                }

                // 푸터
                WriteFooter(writer);
            }
        }

        // 헤더 작성
        private static void WriteHeader(StreamWriter writer, DateTime startDate, DateTime endDate, List<string> logFiles)
        {
            writer.WriteLine("================================================================================");
            writer.WriteLine("                         ACS LOG EXPORT REPORT");
            writer.WriteLine("================================================================================");
            writer.WriteLine();
            writer.WriteLine($"Export 날짜: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"검색 기간: {startDate:yyyy-MM-dd} ~ {endDate:yyyy-MM-dd}");
            writer.WriteLine($"총 파일 수: {logFiles.Count}개");
            writer.WriteLine();
            writer.WriteLine("--- 포함된 로그 파일 목록 ---");

            for (int i = 0; i < logFiles.Count; i++)
            {
                var fileInfo = new FileInfo(logFiles[i]);
                writer.WriteLine($"  [{i + 1}] {fileInfo.Name}");
            }

            writer.WriteLine();
            writer.WriteLine("================================================================================");
            writer.WriteLine();
        }

        // 로그 파일 내용 작성
        private static void WriteLogFile(StreamWriter writer, string logFile)
        {
            var fileName = Path.GetFileName(logFile);

            writer.WriteLine();
            writer.WriteLine("################################################################################");
            writer.WriteLine($"### 파일: {fileName}");
            writer.WriteLine("################################################################################");
            writer.WriteLine();

            // 인코딩 자동 감지하여 읽기
            string content = ReadLogFile(logFile);
            writer.Write(content);

            if (!content.EndsWith("\n"))
                writer.WriteLine();

            writer.WriteLine();
            writer.WriteLine($"### [끝] {fileName}");
            writer.WriteLine();
        }

        // 푸터 작성
        private static void WriteFooter(StreamWriter writer)
        {
            writer.WriteLine();
            writer.WriteLine("================================================================================");
            writer.WriteLine("                         LOG EXPORT COMPLETED");
            writer.WriteLine($"                         {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine("================================================================================");
        }

        /// <summary>
        /// 로그 파일 읽기 (UTF-8 인코딩)
        /// Spring Boot 로그는 기본적으로 UTF-8 인코딩 사용
        /// </summary>
        private static string ReadLogFile(string filePath)
        {
            try
            {
                // Spring Boot 로그는 UTF-8이 표준
                return File.ReadAllText(filePath, new UTF8Encoding(false));
            }
            catch
            {
                // 읽기 실패 시 빈 문자열 반환
                return string.Empty;
            }
        }
    }
}