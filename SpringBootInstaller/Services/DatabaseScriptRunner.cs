using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SpringBootInstaller.Services
{
    public class DatabaseScriptRunner
    {
        private readonly LogManager _logger;
        private const string SqlCmdPath = @"C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE";

        public DatabaseScriptRunner()
        {
            _logger = LogManager.Instance;
        }

        public async Task<bool> ExecuteScriptsAsync(string saPassword, string appUserId, string appPassword, string? scriptsPath = null, bool isDryRun = false)
        {
            try
            {
                _logger.Info("데이터베이스 스크립트 실행 시작");

                if (isDryRun)
                {
                    _logger.Info("[DRY-RUN] 데이터베이스 스크립트 실행 시뮬레이션 모드");
                    await Task.Delay(3000);
                    _logger.Success("[DRY-RUN] 데이터베이스 스크립트 실행 완료 (시뮬레이션)");
                    _logger.Success($"[DRY-RUN] 애플리케이션 DB 사용자 생성: {appUserId}");
                    return true;
                }

                // 1. scripts 폴더 확인 (사용자 지정 경로 또는 기본 경로)
                string scriptsFolder = scriptsPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts");

                if (!Directory.Exists(scriptsFolder))
                {
                    _logger.Warning($"scripts 폴더가 존재하지 않습니다: {scriptsFolder}");
                    _logger.Info("스크립트 실행 건너뛰기");
                    return true; // 스크립트가 없어도 성공으로 처리
                }

                _logger.Info($"스크립트 폴더: {scriptsFolder}");

                // 2. .sql 파일 목록 가져오기 (알파벳 순서)
                var scriptFiles = Directory.GetFiles(scriptsFolder, "*.sql")
                    .OrderBy(f => f)
                    .ToArray();

                if (scriptFiles.Length == 0)
                {
                    _logger.Warning("실행할 SQL 스크립트가 없습니다.");
                    return true;
                }

                _logger.Info($"총 {scriptFiles.Length}개의 SQL 스크립트 발견");

                // 3. sqlcmd 경로 확인
                string sqlcmdPath = FindSqlCmd();
                if (string.IsNullOrEmpty(sqlcmdPath))
                {
                    _logger.Error("SQLCMD.EXE를 찾을 수 없습니다.");
                    return false;
                }

                _logger.Info($"SQLCMD 경로: {sqlcmdPath}");

                // 4. 각 스크립트 실행
                for (int i = 0; i < scriptFiles.Length; i++)
                {
                    string scriptFile = scriptFiles[i];
                    string fileName = Path.GetFileName(scriptFile);

                    _logger.Info($"[{i + 1}/{scriptFiles.Length}] 스크립트 실행 중: {fileName}");

                    bool success = await ExecuteSingleScriptAsync(sqlcmdPath, scriptFile, "sa", saPassword);

                    if (!success)
                    {
                        _logger.Error($"스크립트 실행 실패: {fileName}");
                        return false;
                    }

                    _logger.Success($"스크립트 실행 완료: {fileName}");
                }

                _logger.Success($"모든 데이터베이스 스크립트 실행 완료 ({scriptFiles.Length}개)");

                // 5. 애플리케이션 DB 사용자 생성
                _logger.Info($"애플리케이션 DB 사용자 생성 중: {appUserId}");
                bool userCreated = await CreateAppUserAsync(sqlcmdPath, "sa", saPassword, appUserId, appPassword);

                if (!userCreated)
                {
                    _logger.Error("애플리케이션 DB 사용자 생성 실패");
                    return false;
                }

                _logger.Success($"애플리케이션 DB 사용자 생성 완료: {appUserId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("데이터베이스 스크립트 실행 중 오류 발생", ex);
                return false;
            }
        }

        private async Task<bool> ExecuteSingleScriptAsync(string sqlcmdPath, string scriptFile, string saUserId, string saPassword)
        {
            try
            {
                // sqlcmd 명령어 구성
                // -S: 서버 이름
                // -U: 사용자 ID
                // -P: 비밀번호
                // -i: 입력 스크립트 파일
                // -b: 오류 발생 시 배치 중단

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = sqlcmdPath,
                    Arguments = $"-S localhost\\SQLEXPRESS -U {saUserId} -P \"{saPassword}\" -i \"{scriptFile}\" -b",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    _logger.Error("sqlcmd 프로세스를 시작할 수 없습니다.");
                    return false;
                }

                // 출력 및 에러 로그 수집
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    _logger.Info($"[SQLCMD Output] {output.Trim()}");
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    _logger.Warning($"[SQLCMD Error] {error.Trim()}");
                }

                int exitCode = process.ExitCode;

                if (exitCode == 0)
                {
                    return true;
                }
                else
                {
                    _logger.Error($"sqlcmd 실행 실패. Exit Code: {exitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"스크립트 실행 중 예외 발생: {Path.GetFileName(scriptFile)}", ex);
                return false;
            }
        }

        private string FindSqlCmd()
        {
            // SQL Server 설치 경로에서 sqlcmd 찾기
            var possiblePaths = new[]
            {
                @"C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE",
                @"C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE",
                @"C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\190\Tools\Binn\SQLCMD.EXE",
                @"C:\Program Files\Microsoft SQL Server\160\Tools\Binn\SQLCMD.EXE",
                @"C:\Program Files\Microsoft SQL Server\150\Tools\Binn\SQLCMD.EXE",
                @"C:\Program Files\Microsoft SQL Server\140\Tools\Binn\SQLCMD.EXE"
            };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _logger.Info($"SQLCMD 발견: {path}");
                    return path;
                }
            }

            _logger.Warning("기본 경로에서 SQLCMD를 찾을 수 없습니다. PATH 환경변수에서 검색합니다.");

            // PATH 환경변수에서 찾기
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathEnv.Split(';'))
            {
                string fullPath = Path.Combine(dir.Trim(), "SQLCMD.EXE");
                if (File.Exists(fullPath))
                {
                    _logger.Info($"SQLCMD 발견 (PATH): {fullPath}");
                    return fullPath;
                }
            }

            return string.Empty;
        }

        private async Task<bool> CreateAppUserAsync(string sqlcmdPath, string saUserId, string saPassword, string appUserId, string appPassword)
        {
            try
            {
                _logger.Info($"애플리케이션 DB 사용자 생성: {appUserId}");

                // SQL 스크립트: 로그인 생성 및 권한 부여
                string createUserScript = $@"
-- 기존 로그인이 있으면 제거
IF EXISTS (SELECT * FROM sys.server_principals WHERE name = '{appUserId}')
BEGIN
    DROP LOGIN [{appUserId}];
END

-- 새 로그인 생성
CREATE LOGIN [{appUserId}] WITH PASSWORD = '{appPassword}', CHECK_POLICY = OFF;

-- sysadmin 역할 부여 (모든 권한)
ALTER SERVER ROLE sysadmin ADD MEMBER [{appUserId}];
";

                // 임시 SQL 파일 생성
                string tempSqlFile = Path.Combine(Path.GetTempPath(), $"create_user_{Guid.NewGuid()}.sql");
                await File.WriteAllTextAsync(tempSqlFile, createUserScript);

                try
                {
                    // sqlcmd로 실행
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = sqlcmdPath,
                        Arguments = $"-S localhost -U {saUserId} -P \"{saPassword}\" -i \"{tempSqlFile}\" -b",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(processStartInfo);
                    if (process == null)
                    {
                        _logger.Error("sqlcmd 프로세스를 시작할 수 없습니다.");
                        return false;
                    }

                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        _logger.Info($"[Create User Output] {output.Trim()}");
                    }

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        _logger.Warning($"[Create User Error] {error.Trim()}");
                    }

                    int exitCode = process.ExitCode;
                    return exitCode == 0;
                }
                finally
                {
                    // 임시 파일 삭제
                    if (File.Exists(tempSqlFile))
                    {
                        File.Delete(tempSqlFile);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"애플리케이션 사용자 생성 중 예외 발생: {appUserId}", ex);
                return false;
            }
        }
    }
}

