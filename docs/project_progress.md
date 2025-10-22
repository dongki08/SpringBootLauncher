# SpringBootLauncher 프로젝트 진행 상황

## 프로젝트 개요
- **위치**: C:\Users\dongki\RiderProjects\SpringBootLauncher
- **기술 스택**: WPF, .NET 8.0, C#
- **목적**: Spring Boot JAR 관리 및 자동 설치 시스템

---

## 완료된 작업

### 1. SpringBootLauncher (메인 애플리케이션)

#### 기능
- Spring Boot JAR 파일 실행 및 관리
- 서버 시작/중지 제어
- 실시간 로그 모니터링
- Windows 시작 시 자동 실행 기능
- 체크박스 선택 시 자동 서버 시작
- 시스템 트레이 최소화 지원

#### 핵심 파일
- `MainWindow.xaml` - 메인 UI (JAR 경로, 시작/중지 버튼, 로그 뷰어)
- `MainWindow.xaml.cs` - 비즈니스 로직
  - Windows 시작 프로그램 등록 (Registry)
  - 자동 서버 시작 로직
  - 프로세스 관리
- `App.xaml.cs` - 앱 초기화, `--minimized` 인자 처리
- `SettingsManager.cs` - 설정 저장/로드 (%AppData%)
- `NotificationWindow.xaml` - 토스트 알림 UI
- `UniversalDialog.xaml` - 범용 다이얼로그

#### 주요 기술 구현
```csharp
// Windows 시작 프로그램 등록
private void RegisterStartup()
{
    using var key = Registry.CurrentUser.OpenSubKey(
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
    key.SetValue(AppName, $"\"{exePath}\" --minimized");
}

// 자동 서버 시작
if (AutoStartCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(jarPath))
{
    await Task.Delay(1000);
    StartServer();
}
```

---

### 2. SpringBootInstaller (자동 설치 프로그램)

#### 프로젝트 구조
```
SpringBootInstaller/
├── App.xaml              # 리소스 정의, 시작점
├── App.xaml.cs           # 관리자 권한 체크
├── app.manifest          # 관리자 권한 요구 설정
├── SpringBootInstaller.csproj
├── Models/
│   └── InstallConfig.cs  # 설치 설정 모델
├── Windows/
│   ├── WelcomeWindow.xaml/cs       # 시작 화면
│   ├── SettingsWindow.xaml/cs      # SA 비밀번호 입력
│   ├── ProgressWindow.xaml/cs      # 설치 진행 상황
│   └── CompleteWindow.xaml/cs      # 완료 화면
└── Services/
    ├── LogManager.cs               # 로그 관리 (싱글톤)
    ├── InstallService.cs           # 메인 오케스트레이터
    ├── JavaInstaller.cs            # Java 설치
    ├── EnvironmentSetup.cs         # 환경변수 설정
    ├── MSSQLInstaller.cs           # MSSQL 설치
    ├── DatabaseScriptRunner.cs     # SQL 스크립트 실행
    └── ApplicationInstaller.cs     # 앱 설치 및 바로가기
```

#### 설치 프로세스 (8단계)
1. **관리자 권한 확인** (5%)
2. **Java 설치** (15%) - openjdk-17_windows-x64_bin.zip → C:\java
3. **환경변수 설정** (25%) - JAVA_HOME, PATH
4. **MSSQL 설치** (40%) - SQL2022-SSEI-Dev.exe (무인 설치)
5. **SQL Server 서비스 대기** (60%)
6. **DB 스크립트 실행** (75%) - scripts/*.sql (알파벳 순)
7. **애플리케이션 설치** (90%) - SpringBootLauncher.exe, myapp.jar
8. **바탕화면 바로가기** (95%)

#### 주요 클래스 세부 내용

##### InstallConfig.cs
```csharp
public class InstallConfig
{
    public string SqlPassword { get; set; }
    public string InstallPath { get; set; } = @"C:\Program Files\MyApp";
    public string JavaPath => @"C:\java";  // 고정
    public string SqlUserId => "sa";       // 고정
    public string SqlServer => "localhost\\SQLEXPRESS";
}
```

##### LogManager.cs
- 싱글톤 패턴
- 로그 파일: `C:\MyAppInstall\Logs\install_log_{timestamp}.txt`
- 메서드: `Info()`, `Success()`, `Warning()`, `Error()`
- 스레드 안전 파일 쓰기 (`lock`)

##### JavaInstaller.cs
```csharp
public async Task<bool> InstallAsync(string targetPath)
{
    // 1. ZIP 파일 확인 (installers/openjdk-17_windows-x64_bin.zip)
    // 2. C:\java 폴더 생성
    // 3. ZIP 압축 해제
    // 4. jdk-17.0.x 폴더 내용을 C:\java로 이동
    // 5. java.exe 확인
}
```

##### EnvironmentSetup.cs
```csharp
public async Task<bool> SetupAsync(string javaPath)
{
    // 1. JAVA_HOME 환경변수 설정 (HKLM\SYSTEM\...\Environment)
    // 2. PATH에 %JAVA_HOME%\bin 추가
    // 3. WM_SETTINGCHANGE 브로드캐스트 (Win32 API)
}
```

##### MSSQLInstaller.cs
```csharp
private string BuildInstallArguments(string saPassword)
{
    return "/Q /ACTION=Install /FEATURES=SQLEngine " +
           "/INSTANCENAME=SQLEXPRESS /SECURITYMODE=SQL " +
           "/SAPWD=\"{saPassword}\" /TCPENABLED=1 ...";
}

public async Task<bool> WaitForServiceAsync()
{
    // MSSQL$SQLEXPRESS 서비스 상태 확인 (최대 5분 대기)
    // ServiceController 사용
}
```

##### DatabaseScriptRunner.cs
```csharp
public async Task<bool> ExecuteScriptsAsync(string saPassword)
{
    // 1. scripts/ 폴더에서 *.sql 파일 검색
    // 2. 알파벳 순으로 정렬
    // 3. SQLCMD.EXE로 각 스크립트 실행
    //    sqlcmd -S localhost\SQLEXPRESS -U sa -P "pwd" -i "script.sql"
}
```

##### ApplicationInstaller.cs
```csharp
public async Task<bool> InstallAsync(string installPath)
{
    // app/ 폴더의 모든 파일을 installPath로 복사
}

public async Task<bool> CreateShortcutAsync(string installPath)
{
    // IWshRuntimeLibrary 사용하여 바탕화면 바로가기 생성
}
```

#### UI 화면 세부 내용

##### WelcomeWindow.xaml
- 설치 항목 표시
  - OpenJDK 17
  - SQL Server 2022 Developer
  - Spring Boot Launcher
- 시스템 요구사항 안내
- "다음" 버튼 → SettingsWindow

##### SettingsWindow.xaml
- SA 비밀번호 입력 (실시간 유효성 검사)
  - 최소 8자
  - 대문자 포함
  - 소문자 포함
  - 숫자 포함
  - 특수문자 포함
- 비밀번호 확인 입력
- 설치 경로 선택 (기본: C:\Program Files\MyApp)
- 요구사항 충족 시에만 "다음" 버튼 활성화

##### ProgressWindow.xaml
- 진행률 표시 (퍼센트 + 프로그레스 바)
- 현재 작업 메시지
- 작업 목록 (8개)
  - ⏸ 대기 (회색)
  - ⏳ 진행 중 (파란색)
  - ✓ 완료 (녹색)
  - ❌ 실패 (빨간색)
- 설치 중 창 닫기 방지

##### CompleteWindow.xaml
- 설치 성공 메시지
- 설치된 구성 요소 요약
- 접속 정보 표시
  - Java 경로
  - SQL Server 정보
  - 앱 설치 경로
- "프로그램 실행" / "종료" 버튼

---

## 필요한 설치 파일 구조

```
SpringBootInstaller/
├── installers/
│   ├── openjdk-17_windows-x64_bin.zip    # Java 설치 파일
│   └── SQL2022-SSEI-Dev.exe              # MSSQL 설치 파일
├── scripts/
│   ├── 01_create_database.sql            # 사용자 제공
│   └── 02_create_tables.sql              # 사용자 제공
└── app/
    ├── SpringBootLauncher.exe
    ├── myapp.jar
    └── (기타 필요한 파일들)
```

---

## 기술적 특징

### 관리자 권한 요구 (app.manifest)
```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```

### 이벤트 기반 진행 상황 업데이트
```csharp
public event Action<int, string>? ProgressChanged;
public event Action<int, TaskStatus>? TaskStatusChanged;

installer.ProgressChanged += (progress, message) => {
    Dispatcher.Invoke(() => UpdateProgress(progress, message));
};
```

### 에러 처리 및 로깅
- 모든 작업에 try-catch 블록
- 상세한 로그 기록 (C:\MyAppInstall\Logs\)
- 설치 실패 시 사용자에게 로그 경로 안내

### 무인 설치 지원
- MSSQL: Silent 모드 (/Q)
- 사용자 입력 최소화 (SA 비밀번호만)
- 진행 상황 자동 업데이트

---

## 다음 단계 (미완성)

1. **SpringBootInstaller.csproj 프로젝트 파일 생성**
   - COM 참조 추가 (IWshRuntimeLibrary)
   - app.manifest 링크
   - 기타 NuGet 패키지

2. **솔루션 파일 업데이트**
   - SpringBootLauncher.sln에 SpringBootInstaller 프로젝트 추가

3. **실제 파일 준비**
   - Java ZIP 다운로드
   - MSSQL 설치 파일 다운로드
   - 테스트용 SQL 스크립트 작성

4. **빌드 및 테스트**
   - Release 모드 빌드
   - 실제 설치 테스트
   - 에러 처리 검증

5. **배포 패키지 구성**
   - 모든 필요 파일 포함
   - 압축 파일 생성
   - README 작성

---

## 파일 다운로드 링크

### OpenJDK 17
- URL: https://download.java.net/java/GA/jdk17.0.2/dfd4a8d0985749f896bed50d7138ee7f/8/GPL/openjdk-17.0.2_windows-x64_bin.zip
- 저장 위치: `installers/openjdk-17_windows-x64_bin.zip`

### SQL Server 2022 Developer
- URL: https://go.microsoft.com/fwlink/p/?linkid=2215158
- 파일명: SQL2022-SSEI-Dev.exe
- 저장 위치: `installers/SQL2022-SSEI-Dev.exe`

---

## 코드 통계

### SpringBootLauncher
- MainWindow.xaml.cs: ~600 lines
- App.xaml.cs: ~120 lines
- SettingsManager.cs: ~80 lines

### SpringBootInstaller
- InstallService.cs: ~155 lines
- LogManager.cs: ~107 lines
- JavaInstaller.cs: ~112 lines
- EnvironmentSetup.cs: ~172 lines
- MSSQLInstaller.cs: ~184 lines
- DatabaseScriptRunner.cs: ~191 lines
- ApplicationInstaller.cs: ~150 lines

**총 라인 수: ~1,900+ lines**

---

## 주요 기술 스택 요약

- **UI Framework**: WPF (XAML)
- **Runtime**: .NET 8.0
- **Language**: C# 12
- **Async Pattern**: async/await
- **Process Management**: System.Diagnostics.Process
- **Registry**: Microsoft.Win32.Registry
- **Service Control**: System.ServiceProcess.ServiceController
- **Compression**: System.IO.Compression.ZipFile
- **COM Interop**: IWshRuntimeLibrary (바로가기)
- **Win32 API**: P/Invoke (환경변수 알림)

---

**최종 업데이트**: 2025-10-22
**상태**: 코드 구현 완료, 프로젝트 파일 및 테스트 필요
