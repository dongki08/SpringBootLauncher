namespace SpringBootInstaller.Models
{
    /// <summary>
    /// 설치 설정 정보
    /// </summary>
    public class InstallConfig
    {
        /// <summary>
        /// MSSQL SA 계정 비밀번호 (내부 사용, 고정값)
        /// </summary>
        public string SaPassword => "ACSAdmin123!@#";

        /// <summary>
        /// 애플리케이션용 DB 사용자 아이디
        /// </summary>
        public string AppUserId { get; set; } = string.Empty;

        /// <summary>
        /// 애플리케이션용 DB 사용자 비밀번호
        /// </summary>
        public string AppPassword { get; set; } = string.Empty;

        /// <summary>
        /// DB 스크립트 폴더 경로 (선택사항)
        /// </summary>
        public string? ScriptsPath { get; set; } = null;

        /// <summary>
        /// Java 설치 경로 (고정)
        /// </summary>
        public string JavaPath => @"C:\java";

        /// <summary>
        /// MSSQL 인스턴스명 (기본 인스턴스 MSSQLSERVER 고정)
        /// </summary>
        public string SqlInstanceName => "";

        /// <summary>
        /// MSSQL 서버 주소 (기본 인스턴스: localhost)
        /// </summary>
        public string SqlServer => "localhost";

        /// <summary>
        /// 드라이런 모드 (실제 설치 없이 시뮬레이션만)
        /// </summary>
        public bool IsDryRun { get; set; } = false;
    }
}
