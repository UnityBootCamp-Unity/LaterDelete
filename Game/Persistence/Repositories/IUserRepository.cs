using Game.Persistence.Entities;

namespace Game.Persistence.Repositories
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetAllAsync();
        Task<User> GetByUsernameAsync(string username, CancellationToken ct);
        Task<User> GetByIdAsync(Guid id, CancellationToken ct);
        Task InsertAsync(User user, CancellationToken ct);
        Task UpdateAsync(User user, CancellationToken ct);
        Task DeleteAsync(Guid id, CancellationToken ct);
        Task SaveAsync(CancellationToken ct);
    }
}
