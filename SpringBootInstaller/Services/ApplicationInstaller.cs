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

        public async Task<bool> InstallAsync(string installPath)
        {
            try
            {
                _logger.Info($"애플리케이션 설치 시작: {installPath}");

                // 1. 설치 경로 생성
                if (!Directory.Exists(installPath))
                {
                    Directory.CreateDirectory(installPath);
                    _logger.Info($"설치 폴더 생성: {installPath}");
                }

                // 2. app 폴더에서 파일 복사
                string appFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app");

                if (!Directory.Exists(appFolder))
                {
                    _logger.Warning($"app 폴더가 존재하지 않습니다: {appFolder}");
                    _logger.Info("애플리케이션 파일 복사 건너뛰기");
                    return true; // app 폴더가 없어도 성공으로 처리
                }

                _logger.Info($"app 폴더 확인: {appFolder}");

                // 3. 모든 파일 복사
                await Task.Run(() =>
                {
                    CopyDirectory(appFolder, installPath);
                });

                _logger.Success($"애플리케이션 파일 복사 완료: {installPath}");

                // 4. 실행 파일 확인
                string exePath = Path.Combine(installPath, "SpringBootLauncher.exe");
                string jarPath = Path.Combine(installPath, "myapp.jar");

                if (System.IO.File.Exists(exePath))
                {
                    _logger.Info($"실행 파일 확인: {exePath}");
                }

                if (System.IO.File.Exists(jarPath))
                {
                    _logger.Info($"JAR 파일 확인: {jarPath}");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("애플리케이션 설치 중 오류 발생", ex);
                return false;
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            // 디렉토리 생성
            Directory.CreateDirectory(destDir);

            // 파일 복사
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destDir, fileName);
                System.IO.File.Copy(file, destFile, overwrite: true);
                _logger.Info($"파일 복사: {fileName}");
            }

            // 하위 디렉토리 복사 (재귀)
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(dir);
                string destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(dir, destSubDir);
            }
        }

        public async Task<bool> CreateShortcutAsync(string installPath)
        {
            try
            {
                _logger.Info("바탕화면 바로가기 생성 시작");

                await Task.Run(() =>
                {
                    // 바탕화면 경로
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string shortcutPath = Path.Combine(desktopPath, "Spring Boot Launcher.lnk");

                    // 실행 파일 경로
                    string exePath = Path.Combine(installPath, "SpringBootLauncher.exe");

                    if (!System.IO.File.Exists(exePath))
                    {
                        _logger.Warning($"실행 파일을 찾을 수 없습니다: {exePath}");
                        return;
                    }

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
                        shortcut.WorkingDirectory = installPath;
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

