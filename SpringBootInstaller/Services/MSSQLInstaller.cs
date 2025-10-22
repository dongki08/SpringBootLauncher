using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace SpringBootInstaller.Services
{
    public class MSSQLInstaller
    {
        private readonly LogManager _logger;
        private const string ServiceName = "MSSQL$SQLEXPRESS";
        private const int MaxWaitTimeSeconds = 300; // 5분

        public MSSQLInstaller()
        {
            _logger = LogManager.Instance;
        }

        public async Task<bool> InstallAsync(string saUserId, string saPassword, bool isDryRun = false)
        {
            try
            {
                _logger.Info("MSSQL Developer Edition 설치 시작");

                if (isDryRun)
                {
                    _logger.Info("[DRY-RUN] MSSQL 설치 시뮬레이션 모드");
                    await Task.Delay(5000); // 시뮬레이션 지연
                    _logger.Success("[DRY-RUN] MSSQL 설치 완료 (시뮬레이션)");
                    return true;
                }

                // 1. CAB 설치 파일 경로 확인
                string installerFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installers");
                string setupExePath = Path.Combine(installerFolder, "SQLServer2022-DEV-x64-KOR.exe");
                string boxFilePath = Path.Combine(installerFolder, "SQLServer2022-DEV-x64-KOR.box");

                if (!File.Exists(setupExePath))
                {
                    _logger.Error($"MSSQL 설치 실행 파일을 찾을 수 없습니다: {setupExePath}");
                    return false;
                }

                if (!File.Exists(boxFilePath))
                {
                    _logger.Error($"MSSQL CAB 파일을 찾을 수 없습니다: {boxFilePath}");
                    return false;
                }

                _logger.Info($"MSSQL 설치 파일 확인: {setupExePath}");
                _logger.Info($"MSSQL CAB 파일 확인: {boxFilePath}");

                // 2. 설치 실행 (무인 설치)
                _logger.Info("MSSQL 설치 실행 중... (10~15분 소요 예상)");

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = setupExePath,
                    Arguments = BuildInstallArguments(saUserId, saPassword),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _logger.Info($"설치 명령: {processStartInfo.FileName} {processStartInfo.Arguments}");

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    _logger.Error("MSSQL 설치 프로세스를 시작할 수 없습니다.");
                    return false;
                }

                // 출력 로그 기록
                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        _logger.Info($"[MSSQL Setup] {args.Data}");
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        _logger.Warning($"[MSSQL Setup Error] {args.Data}");
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 설치 완료 대기
                await process.WaitForExitAsync();

                int exitCode = process.ExitCode;
                _logger.Info($"MSSQL 설치 완료. Exit Code: {exitCode}");

                if (exitCode == 0 || exitCode == 3010) // 0: 성공, 3010: 재부팅 필요하지만 성공
                {
                    _logger.Success("MSSQL 설치 성공");
                    return true;
                }
                else
                {
                    _logger.Error($"MSSQL 설치 실패. Exit Code: {exitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("MSSQL 설치 중 오류 발생", ex);
                return false;
            }
        }

        private string BuildInstallArguments(string saUserId, string saPassword)
        {
            // SQL Server 2022 Developer Edition 무인 설치 파라미터
            // /Q: Quiet 모드 (UI 없음)
            // /ACTION=Install: 설치 작업
            // /FEATURES=SQLEngine: SQL 엔진만 설치
            // /INSTANCENAME=SQLEXPRESS: 인스턴스 이름
            // /SECURITYMODE=SQL: SQL 인증 모드 활성화
            // /SAPWD: SA 계정 비밀번호
            // /SQLSVCACCOUNT: SQL 서비스 계정 (NT AUTHORITY\SYSTEM)
            // /SQLSYSADMINACCOUNTS: 관리자 권한 계정
            // /IACCEPTSQLSERVERLICENSETERMS: 라이선스 동의
            // /TCPENABLED=1: TCP/IP 프로토콜 활성화
            // 참고: SA 계정 아이디는 설치 후 별도로 변경할 수 없으므로, 기본값 'sa' 사용

            _logger.Info($"SA 계정 아이디: {saUserId} (참고: MSSQL 설치 시 SA 이름은 항상 'sa'로 고정됨)");

            return $"/Q " +
                   $"/ACTION=Install " +
                   $"/FEATURES=SQLEngine " +
                   $"/INSTANCENAME=SQLEXPRESS " +
                   $"/SECURITYMODE=SQL " +
                   $"/SAPWD=\"{saPassword}\" " +
                   $"/SQLSVCACCOUNT=\"NT AUTHORITY\\SYSTEM\" " +
                   $"/SQLSYSADMINACCOUNTS=\"BUILTIN\\Administrators\" " +
                   $"/IACCEPTSQLSERVERLICENSETERMS " +
                   $"/TCPENABLED=1 " +
                   $"/UPDATEENABLED=0";
        }

        public async Task<bool> WaitForServiceAsync(bool isDryRun = false)
        {
            try
            {
                _logger.Info($"SQL Server 서비스 시작 대기 중... (서비스명: {ServiceName})");

                if (isDryRun)
                {
                    _logger.Info("[DRY-RUN] SQL Server 서비스 대기 시뮬레이션 모드");
                    await Task.Delay(2000);
                    _logger.Success("[DRY-RUN] SQL Server 서비스 실행 확인 (시뮬레이션)");
                    return true;
                }

                int elapsedSeconds = 0;

                while (elapsedSeconds < MaxWaitTimeSeconds)
                {
                    try
                    {
                        using var service = new ServiceController(ServiceName);

                        if (service.Status == ServiceControllerStatus.Running)
                        {
                            _logger.Success($"SQL Server 서비스가 실행 중입니다. (대기 시간: {elapsedSeconds}초)");
                            return true;
                        }

                        if (service.Status == ServiceControllerStatus.Stopped)
                        {
                            _logger.Info("SQL Server 서비스가 중지되어 있습니다. 시작을 시도합니다...");
                            service.Start();
                            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
                            _logger.Success("SQL Server 서비스 시작 완료");
                            return true;
                        }

                        _logger.Info($"SQL Server 서비스 상태: {service.Status} (대기 중... {elapsedSeconds}/{MaxWaitTimeSeconds}초)");
                    }
                    catch (InvalidOperationException)
                    {
                        // 서비스가 아직 설치되지 않음
                        _logger.Info($"SQL Server 서비스를 찾을 수 없습니다. 재시도 중... ({elapsedSeconds}/{MaxWaitTimeSeconds}초)");
                    }

                    await Task.Delay(5000); // 5초 대기
                    elapsedSeconds += 5;
                }

                _logger.Error($"SQL Server 서비스 시작 대기 시간 초과 ({MaxWaitTimeSeconds}초)");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error("SQL Server 서비스 확인 중 오류 발생", ex);
                return false;
            }
        }
    }
}

