using Microsoft.EntityFrameworkCore;

namespace Game.Persistence
{
    public static class DBConnectionSettings
    {
        public const string CONNECTION =
            "server=127.0.0.1;" +
            "port=3309;" +
            "user=game;" +
            "password=1234;" +
            "database=multiplaygame;";

        // 이런 readonly 를 쓰면 
        // EFCore Migration 할때 
        // ef cli (dotnet ef migration )
        // DbContext 를 어셈블리에서 찾아서 쓰는데, 현재 GameDbContext 가 이 readonly 필드를 참조하고있어서 
        // AutoDetect 가 바로 실행되는데, 이떄 DB 가 실행이 안되어있으면 해당 데이터 가져올수없어서 올바른값으로 초기화가 안됨.
        // 그래서 GameDbContext 빌드를 중단한다.
        // builder.Services.AddDbContext<GameDbContext>(
        // options => options
        //     .UseMySql(
        //         DBConnectionSettings.CONNECTION,
        //         DBConnectionSettings.MYSQL_SERVER_VERSION
        //         )
        //
        // 해결방법
        // 1. 고정 버전 만 쓴다. (DB 접속안함)
        // 2. 디자인타임 팩토리 구현. (DB 런타임에서는 AutoDetect 하고 디자인타임에서는 고정 버전사용)
        public static readonly MySqlServerVersion MYSQL_SERVER_VERSION = new MySqlServerVersion(new Version(8, 4, 6));
    }
}
