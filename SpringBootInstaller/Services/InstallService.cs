using System;
using System.Threading.Tasks;
using SpringBootInstaller.Models;

namespace SpringBootInstaller.Services
{
    public class InstallService
    {
        private readonly InstallConfig _config;
        private readonly LogManager _logger;

        // 이벤트: 진행 상황 변경
        public event Action<int, string>? ProgressChanged;

        // 이벤트: 작업 상태 변경
        public event Action<int, Windows.TaskStatus>? TaskStatusChanged;

        public InstallService(InstallConfig config)
        {
            _config = config;
            _logger = LogManager.Instance;
        }

        public async Task<bool> InstallAsync()
        {
            _logger.Info("설치 시작");

            try
            {
                // 0. 관리자 권한 확인 (이미 App.xaml.cs에서 확인됨)
                await ExecuteTask(0, "관리자 권한 확인", 5, async () =>
                {
                    await Task.Delay(500); // 시뮬레이션
                    return true;
                });

                // 1. Java 설치
                await ExecuteTask(1, "OpenJDK 17 압축 해제 중...", 15, InstallJavaAsync);

                // 2. 환경변수 설정
                await ExecuteTask(2, "환경변수 설정 중 (JAVA_HOME, PATH)...", 25, SetupEnvironmentAsync);

                // 3. MSSQL 설치
                await ExecuteTask(3, "MSSQL Developer 설치 중 (10~15분 소요)...", 40, InstallMSSQLAsync);

                // 4. SQL Server 서비스 대기
                await ExecuteTask(4, "SQL Server 서비스 시작 대기 중...", 60, WaitForSqlServiceAsync);

                // 5. 데이터베이스 스크립트 실행
                await ExecuteTask(5, "데이터베이스 스크립트 실행 중...", 75, ExecuteDatabaseScriptsAsync);

                // 6. 애플리케이션 설치
                await ExecuteTask(6, "애플리케이션 설치 중...", 90, InstallApplicationAsync);

                // 7. 바탕화면 바로가기
                await ExecuteTask(7, "바탕화면 바로가기 생성 중...", 95, CreateDesktopShortcutAsync);

                // 완료
                ReportProgress(100, "설치 완료!");
                _logger.Success("설치 완료!");

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("설치 실패", ex);
                return false;
            }
        }

        private async Task ExecuteTask(int taskIndex, string message, int progressPercentage, Func<Task<bool>> action)
        {
            _logger.Info($"작업 시작: {message}");

            // 작업 시작 상태 변경
            TaskStatusChanged?.Invoke(taskIndex, Windows.TaskStatus.InProgress);
            ReportProgress(progressPercentage, message);

            try
            {
                bool success = await action();

                if (success)
                {
                    // 작업 완료
                    TaskStatusChanged?.Invoke(taskIndex, Windows.TaskStatus.Completed);
                    _logger.Success($"작업 완료: {message}");
                }
                else
                {
                    // 작업 실패
                    TaskStatusChanged?.Invoke(taskIndex, Windows.TaskStatus.Failed);
                    _logger.Error($"작업 실패: {message}");
                    throw new Exception($"{message} 실패");
                }
            }
            catch (Exception ex)
            {
                TaskStatusChanged?.Invoke(taskIndex, Windows.TaskStatus.Failed);
                _logger.Error($"작업 예외: {message}", ex);
                throw;
            }
        }

        private void ReportProgress(int percentage, string message)
        {
            ProgressChanged?.Invoke(percentage, message);
        }

        // ========== 각 설치 단계 메서드 ==========

        private async Task<bool> InstallJavaAsync()
        {
            var javaInstaller = new JavaInstaller();
            return await javaInstaller.InstallAsync(_config.JavaPath, _config.IsDryRun);
        }

        private async Task<bool> SetupEnvironmentAsync()
        {
            var envSetup = new EnvironmentSetup();
            return await envSetup.SetupAsync(_config.JavaPath, _config.IsDryRun);
        }

        private async Task<bool> InstallMSSQLAsync()
        {
            var sqlInstaller = new MSSQLInstaller();
            return await sqlInstaller.InstallAsync(_config.SqlUserId, _config.SqlPassword, _config.IsDryRun);
        }

        private async Task<bool> WaitForSqlServiceAsync()
        {
            var sqlInstaller = new MSSQLInstaller();
            return await sqlInstaller.WaitForServiceAsync(_config.IsDryRun);
        }

        private async Task<bool> ExecuteDatabaseScriptsAsync()
        {
            var dbRunner = new DatabaseScriptRunner();
            return await dbRunner.ExecuteScriptsAsync(_config.SqlUserId, _config.SqlPassword, _config.ScriptsPath, _config.IsDryRun);
        }

        private async Task<bool> InstallApplicationAsync()
        {
            var appInstaller = new ApplicationInstaller();
            return await appInstaller.InstallAsync(_config.InstallPath, _config.IsDryRun);
        }

        private async Task<bool> CreateDesktopShortcutAsync()
        {
            var appInstaller = new ApplicationInstaller();
            return await appInstaller.CreateShortcutAsync(_config.InstallPath, _config.IsDryRun);
        }
    }
}
