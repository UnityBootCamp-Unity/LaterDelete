using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Game.Persistence.Entities.Configurations
{
    /// <summary>
    /// User 엔티티 데이터무결성 보장구문
    /// </summary>
    public class UserConfig : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id);

            builder.HasIndex(u => u.UserName)
                .IsUnique(true); // 중복 유저이름 방지

            // 닉네임 길이 제한 12 글자
            builder.Property(u => u.Nickname)
                .HasMaxLength(12)
                .IsRequired(false);

            builder.HasIndex(u => u.Nickname)
                .IsUnique(true); // 중복 닉네임 방지

            // Create at 생성날짜는 MySQL Timestamp 자동입력
            builder.Property(u => u.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)") // 6자리 ms (마이크로초)
                .IsRequired();

            // Last connected 날짜는 MySQL Timestamp 자동입력, 하지만 필수는 아님
            builder.Property(u => u.LastConnected)
                .IsRequired(false);
        }
    }
}