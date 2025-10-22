using System;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace SpringBootInstaller.Services
{
    public class EnvironmentSetup
    {
        private readonly LogManager _logger;

        public EnvironmentSetup()
        {
            _logger = LogManager.Instance;
        }

        public async Task<bool> SetupAsync(string javaPath)
        {
            try
            {
                _logger.Info($"환경변수 설정 시작: JAVA_HOME = {javaPath}");

                await Task.Run(() =>
                {
                    // 1. JAVA_HOME 환경변수 설정 (사용자 변수)
                    SetUserEnvironmentVariable("JAVA_HOME", javaPath);
                    _logger.Success($"JAVA_HOME 사용자 변수 설정 완료: {javaPath}");

                    // 2. PATH에 C:\java\bin 추가 (시스템 변수)
                    string javaBinPath = @"C:\java\bin";
                    AddToSystemPath(javaBinPath);
                    _logger.Success($"PATH에 Java bin 추가 완료: {javaBinPath}");
                });

                // 3. 환경변수 변경 알림 (시스템에 브로드캐스트)
                NotifyEnvironmentChange();
                _logger.Info("환경변수 변경 알림 전송 완료");

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("환경변수 설정 중 오류 발생", ex);
                return false;
            }
        }

        private void SetUserEnvironmentVariable(string name, string value)
        {
            try
            {
                // HKEY_CURRENT_USER\Environment
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Environment",
                    writable: true
                );

                if (key != null)
                {
                    key.SetValue(name, value, RegistryValueKind.String);
                    _logger.Info($"사용자 환경변수 설정: {name} = {value}");
                }
                else
                {
                    throw new Exception("사용자 환경변수 레지스트리 키를 열 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"사용자 환경변수 설정 실패: {name}", ex);
                throw;
            }
        }

        private void SetSystemEnvironmentVariable(string name, string value)
        {
            try
            {
                // HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment",
                    writable: true
                );

                if (key != null)
                {
                    key.SetValue(name, value, RegistryValueKind.String);
                    _logger.Info($"시스템 환경변수 설정: {name} = {value}");
                }
                else
                {
                    throw new Exception("환경변수 레지스트리 키를 열 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"환경변수 설정 실패: {name}", ex);
                throw;
            }
        }

        private void AddToSystemPath(string newPath)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment",
                    writable: true
                );

                if (key != null)
                {
                    string? currentPath = key.GetValue("Path") as string;

                    if (string.IsNullOrEmpty(currentPath))
                    {
                        // PATH가 없으면 새로 생성
                        key.SetValue("Path", newPath, RegistryValueKind.ExpandString);
                        _logger.Info($"PATH 생성: {newPath}");
                    }
                    else
                    {
                        // 이미 경로가 있는지 확인 (%JAVA_HOME%\bin 또는 C:\java\bin 둘 다 체크)
                        string[] pathEntries = currentPath.Split(';');
                        foreach (string entry in pathEntries)
                        {
                            string trimmedEntry = entry.Trim();
                            // %JAVA_HOME%\bin 또는 C:\java\bin이 이미 있는지 확인
                            if (trimmedEntry.Equals(newPath, StringComparison.OrdinalIgnoreCase) ||
                                trimmedEntry.Equals(@"%JAVA_HOME%\bin", StringComparison.OrdinalIgnoreCase) ||
                                trimmedEntry.Equals(@"C:\java\bin", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.Info($"PATH에 이미 Java bin 경로 존재함: {trimmedEntry}");
                                return;
                            }
                        }

                        // PATH 끝에 추가
                        string updatedPath = currentPath.TrimEnd(';') + ";" + newPath;
                        key.SetValue("Path", updatedPath, RegistryValueKind.ExpandString);
                        _logger.Info($"PATH에 추가: {newPath}");
                    }
                }
                else
                {
                    throw new Exception("PATH 레지스트리 키를 열 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("PATH 추가 실패", ex);
                throw;
            }
        }

        private void NotifyEnvironmentChange()
        {
            try
            {
                // 환경변수 변경을 시스템에 알리기 위한 Win32 API 호출
                // WM_SETTINGCHANGE 메시지 브로드캐스트
                const int HWND_BROADCAST = 0xffff;
                const int WM_SETTINGCHANGE = 0x1a;

                IntPtr result;
                SendMessageTimeout(
                    (IntPtr)HWND_BROADCAST,
                    WM_SETTINGCHANGE,
                    IntPtr.Zero,
                    "Environment",
                    SendMessageTimeoutFlags.SMTO_ABORTIFHUNG,
                    5000,
                    out result
                );
            }
            catch (Exception ex)
            {
                _logger.Warning($"환경변수 변경 알림 전송 실패 (무시 가능): {ex.Message}");
            }
        }

        // Win32 API 선언
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            int Msg,
            IntPtr wParam,
            string lParam,
            SendMessageTimeoutFlags flags,
            uint timeout,
            out IntPtr result
        );

        [System.Flags]
        private enum SendMessageTimeoutFlags : uint
        {
            SMTO_NORMAL = 0x0,
            SMTO_BLOCK = 0x1,
            SMTO_ABORTIFHUNG = 0x2,
            SMTO_NOTIMEOUTIFNOTHUNG = 0x8
        }
    }
}

