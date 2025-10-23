using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SpringBootInstaller.Services
{
    public class ApplicationInstaller
    {
        private readonly LogManager _logger;

        public ApplicationInstaller()
        {
            _logger = LogManager.Instance;
        }

        public async Task<bool> CreateShortcutAsync(bool isDryRun = false)
        {
            try
            {
                _logger.Info("바탕화면 바로가기 생성 시작");

                if (isDryRun)
                {
                    _logger.Info("[DRY-RUN] 바탕화면 바로가기 생성 시뮬레이션 모드");
                    await Task.Delay(1000);
                    _logger.Success("[DRY-RUN] 바탕화면 바로가기 생성 완료 (시뮬레이션)");
                    return true;
                }

                await Task.Run(() =>
                {
                    // 바탕화면 경로
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string shortcutPath = Path.Combine(desktopPath, "Spring Boot Launcher.lnk");

                    // 고정 경로: C:\ACS\server\Launcher\SpringBootLauncher.exe
                    string launcherFolder = @"C:\ACS\server\Launcher";
                    string exePath = Path.Combine(launcherFolder, "SpringBootLauncher.exe");

                    if (!System.IO.File.Exists(exePath))
                    {
                        _logger.Warning($"실행 파일을 찾을 수 없습니다: {exePath}");
                        _logger.Info("SpringBootLauncher.exe가 C:\\ACS\\server\\Launcher\\ 폴더에 있어야 합니다.");
                        return;
                    }

                    _logger.Info($"Launcher 실행 파일 확인: {exePath}");

                    // COM을 사용하여 바로가기 생성
                    Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType == null)
                    {
                        _logger.Warning("WScript.Shell을 찾을 수 없습니다.");
                        return;
                    }

                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell == null)
                    {
                        _logger.Warning("WScript.Shell 인스턴스 생성 실패");
                        return;
                    }

                    try
                    {
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        shortcut.TargetPath = exePath;
                        shortcut.WorkingDirectory = launcherFolder;
                        shortcut.Description = "Spring Boot Launcher";
                        shortcut.IconLocation = exePath + ",0";
                        shortcut.Save();

                        _logger.Success($"바탕화면 바로가기 생성 완료: {shortcutPath}");
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(shell);
                    }
                });

                return true;
            }catch (Exception ex)
            {
                _logger.Error("바탕화면 바로가기 생성 중 오류 발생", ex);
                // 바로가기 생성 실패는 치명적이지 않으므로 true 반환
                return true;
            }
        }
    }
}

