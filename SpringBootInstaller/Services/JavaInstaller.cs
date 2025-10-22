using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace SpringBootInstaller.Services
{
    public class JavaInstaller
    {
        private readonly LogManager _logger;

        public JavaInstaller()
        {
            _logger = LogManager.Instance;
        }

        public async Task<bool> InstallAsync(string targetPath)
        {
            try
            {
                _logger.Info($"Java 설치 시작: 대상 경로 = {targetPath}");

                // 1. 설치 파일 경로 확인
                string installerFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installers");
                string zipFilePath = Path.Combine(installerFolder, "openjdk-17_windows-x64_bin.zip");

                if (!File.Exists(zipFilePath))
                {
                    _logger.Error($"Java 설치 파일을 찾을 수 없습니다: {zipFilePath}");
                    return false;
                }

                _logger.Info($"Java 설치 파일 확인: {zipFilePath}");

                // 2. 대상 폴더 생성
                if (Directory.Exists(targetPath))
                {
                    _logger.Warning($"대상 폴더가 이미 존재합니다. 삭제 후 재생성: {targetPath}");
                    Directory.Delete(targetPath, true);
                }

                Directory.CreateDirectory(targetPath);
                _logger.Info($"대상 폴더 생성 완료: {targetPath}");

                // 3. ZIP 압축 해제
                _logger.Info("ZIP 압축 해제 시작...");
                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(zipFilePath, targetPath);
                });

                _logger.Success("ZIP 압축 해제 완료");

                // 4. 압축 해제 후 폴더 구조 확인 및 정리
                // openjdk-17_windows-x64_bin.zip 압축 해제 시 jdk-17.0.x 폴더가 생성됨
                string[] directories = Directory.GetDirectories(targetPath, "jdk-*");

                if (directories.Length > 0)
                {
                    string jdkFolder = directories[0];
                    _logger.Info($"JDK 폴더 발견: {jdkFolder}");

                    // jdk-17.0.x 폴더의 내용을 C:\java로 이동
                    await MoveDirectoryContents(jdkFolder, targetPath);

                    // 빈 jdk-17.0.x 폴더 삭제
                    Directory.Delete(jdkFolder, false);
                    _logger.Info("JDK 폴더 내용을 대상 경로로 이동 완료");
                }

                // 5. java.exe 확인
                string javaExePath = Path.Combine(targetPath, "bin", "java.exe");
                if (!File.Exists(javaExePath))
                {
                    _logger.Error($"Java 실행 파일을 찾을 수 없습니다: {javaExePath}");
                    return false;
                }

                _logger.Success($"Java 설치 완료: {javaExePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Java 설치 중 오류 발생", ex);
                return false;
            }
        }

        private async Task MoveDirectoryContents(string sourceDir, string targetDir)
        {
            await Task.Run(() =>
            {
                // 파일 이동
                foreach (string file in Directory.GetFiles(sourceDir))
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(targetDir, fileName);
                    File.Move(file, destFile);
                }

                // 폴더 이동
                foreach (string directory in Directory.GetDirectories(sourceDir))
                {
                    string dirName = Path.GetFileName(directory);
                    string destDir = Path.Combine(targetDir, dirName);
                    Directory.Move(directory, destDir);
                }
            });
        }
    }
}

