namespace SpringBootInstaller.Models
{
    /// <summary>
    /// 설치 설정 정보
    /// </summary>
    public class InstallConfig
    {
        /// <summary>
        /// MSSQL SA 계정 아이디
        /// </summary>
        public string SqlUserId { get; set; } = "sa";

        /// <summary>
        /// MSSQL SA 계정 비밀번호
        /// </summary>
        public string SqlPassword { get; set; } = string.Empty;

        /// <summary>
        /// 애플리케이션 설치 경로
        /// </summary>
        public string InstallPath { get; set; } = @"C:\Program Files\MyApp";

        /// <summary>
        /// DB 스크립트 폴더 경로 (선택사항)
        /// </summary>
        public string? ScriptsPath { get; set; } = null;

        /// <summary>
        /// Java 설치 경로 (고정)
        /// </summary>
        public string JavaPath => @"C:\java";

        /// <summary>
        /// MSSQL 인스턴스명
        /// </summary>
        public string SqlInstanceName => "SQLEXPRESS";

        /// <summary>
        /// MSSQL 서버 주소
        /// </summary>
        public string SqlServer => $@"localhost\{SqlInstanceName}";
    }
}
