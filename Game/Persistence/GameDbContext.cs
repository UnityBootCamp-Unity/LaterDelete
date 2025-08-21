using Game.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.Persistence
{
    public class GameDbContext : DbContext
    {
        public GameDbContext(DbContextOptions options) : base(options)
        {
        }


        // Db 쿼리 루트
        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 어셈블리전체에서 IEntityTypeConfiguration 전부 찾아서 적용
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);

            // 이렇게하면 엔티티 하나늘어날때마다 코드 수정해야함.. 엄청길어짐.. 
            // 그래서 위처럼 어셈블리 전체 뒤져서 알아서 구현함
            //modelBuilder.Entity<User>(entity =>
            //{
            //    entity.HasKey(u => u.Id);
            //
            //    entity.HasIndex(u => u.UserName)
            //        .IsUnique(true); // 중복 유저이름 방지
            //
            //    // 닉네임 길이 제한 12 글자
            //    entity.Property(u => u.Nickname)
            //        .HasMaxLength(12)
            //        .IsRequired();
            //
            //    entity.HasIndex(u => u.Nickname)
            //        .IsUnique(true); // 중복 닉네임 방지
            //
            //    // Create at 생성날짜는 MySQL Timestamp 자동입력
            //    entity.Property(u => u.CreatedAt)
            //        .HasDefaultValueSql("CURRENT_TIMESTAMP(6)") // 6자리 ms (마이크로초)
            //        .IsRequired();
            //
            //    // Last connected 날짜는 MySQL Timestamp 자동입력, 하지만 필수는 아님
            //    entity.Property(u => u.LastConnected)
            //        .IsRequired(false);
            //});
        }
    }
}
