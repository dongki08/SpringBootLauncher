using System;
using System.IO;
using System.Text;

namespace SpringBootInstaller.Services
{
    public class LogManager
    {
        private static LogManager? _instance;
        private static readonly object _lock = new object();

        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly object _fileLock = new object();

        // 실시간 로그 이벤트
        public event Action<string, string>? LogAdded;

        private LogManager()
        {
            // 로그 디렉토리: C:\ACS\log\
            _logDirectory = @"C:\ACS\log";
            Directory.CreateDirectory(_logDirectory);

            // 로그 파일명: install.log (단일 파일)
            _logFilePath = Path.Combine(_logDirectory, "install.log");

            // 기존 로그 파일이 있으면 삭제하고 새로 시작
            if (File.Exists(_logFilePath))
            {
                File.Delete(_logFilePath);
            }

            // 초기 로그 기록
            WriteLog("INFO", "설치 프로그램 시작");
            WriteLog("INFO", $"로그 파일: {_logFilePath}");
        }

        public static LogManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LogManager();
                        }
                    }
                }
                return _instance;
            }
        }

        public void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public void Success(string message)
        {
            WriteLog("SUCCESS", message);
        }

        public void Warning(string message)
        {
            WriteLog("WARNING", message);
        }

        public void Error(string message)
        {
            WriteLog("ERROR", message);
        }

        public void Error(string message, Exception ex)
        {
            WriteLog("ERROR", $"{message}: {ex.Message}");
            WriteLog("ERROR", $"Stack Trace: {ex.StackTrace}");
        }

        private void WriteLog(string level, string message)
        {
            try
            {
                lock (_fileLock)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string logLine = $"[{timestamp}] [{level}] {message}";

                    File.AppendAllText(_logFilePath, logLine + Environment.NewLine, Encoding.UTF8);

                    // 디버그 출력 (개발 중 확인용)
                    System.Diagnostics.Debug.WriteLine(logLine);

                    // 실시간 로그 이벤트 발생
                    LogAdded?.Invoke(level, message);
                }
            }
            catch
            {
                // 로그 쓰기 실패 시 무시 (프로그램 중단 방지)
            }
        }

        public string GetLogFilePath()
        {
            return _logFilePath;
        }

        public string GetLogDirectory()
        {
            return _logDirectory;
        }
    }
}
