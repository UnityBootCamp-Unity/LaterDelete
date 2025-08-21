using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Game.Persistence.DesignTimeFactories
{
    /// <summary>
    /// 디자인타임에서 사용할거 (EF CLI 로 쓸때.. dotnet ef migration .. 이런거)
    /// 디자인타임에서는 고정 버전사용
    /// EF CLI 가 리플렉션으로 자동으로 이거 찾아서 사용하기때문에 별도로 사용하는 코드 작성 필요 X
    /// </summary>
    public class GameDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<GameDbContext>()
                .UseMySql(
                    DBConnectionSettings.CONNECTION,
                    DBConnectionSettings.MYSQL_SERVER_VERSION
                    )
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
                .Options;

            return new GameDbContext(options);
        }
    }
}
