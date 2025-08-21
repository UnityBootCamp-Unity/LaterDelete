using Game.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.Persistence.Repositories
{
    /*
     * 모든 함수 예외처리 다 해야하는데 일단 생략해놓은것임 (복습시 가독성 때문에)
     */
    public class UserRepository : IUserRepository, IDisposable
    {
        public UserRepository(GameDbContext context) 
        {
            _context = context;
        }


        private GameDbContext _context;

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            var user = await GetByIdAsync(id, ct);

            if (user == null)
                throw new InvalidOperationException($"Failed to delete user. does not exist");

            _context.Users.Remove(user);
        }


        public async Task<IEnumerable<User>> GetAllAsync() =>
            await _context.Users.ToListAsync();

        public async Task<User> GetByIdAsync(Guid id, CancellationToken ct) => 
            await _context.Users.FindAsync(new object[] { id }, ct);

        public async Task<User> GetByUsernameAsync(string username, CancellationToken ct) =>
            await _context.Users.FirstOrDefaultAsync(u => u.UserName.Equals(username), ct);

        public async Task InsertAsync(User user, CancellationToken ct) =>
            await _context.Users.AddAsync(user, ct);

        public async Task SaveAsync(CancellationToken ct) =>
            await _context.SaveChangesAsync(ct);

        public async Task UpdateAsync(User user, CancellationToken ct)
        {
            var existingUser = await GetByIdAsync(user.Id, ct);

            if (existingUser == null)
                throw new InvalidOperationException($"User with Id{user.Id} not found.");

            existingUser.UserName = user.UserName;
            existingUser.Password = user.Password;
            existingUser.Nickname = user.Nickname;
            existingUser.LastConnected = user.LastConnected;

            _context.Users.Update(existingUser);
        }


        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    _context.Dispose();
                }
            }
            this._disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
