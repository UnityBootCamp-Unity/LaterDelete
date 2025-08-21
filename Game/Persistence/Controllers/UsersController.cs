using Game.Persistence.Entities;
using Game.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Game.Persistence.Controllers
{
    [ApiController]
    [Route("users")]
    public class UsersController : ControllerBase
    {
        public UsersController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        private readonly IUserRepository _userRepository;

        [HttpGet("getall")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _userRepository.GetAllAsync();

                var responses =
                    users.Select(u => new UserResponseDTO
                    (
                        id: u.Id,
                        userName: u.UserName,
                        nickname: u.Nickname,
                        createdAt: u.CreatedAt,
                        lastConnected: u.LastConnected
                    ));

                return Ok(responses);

            }
            catch (Exception ex) 
            {
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        [HttpGet("getallsummaries")]
        public async Task<IActionResult> GetAllUserSummaries()
        {
            try
            {
                var users = await _userRepository.GetAllAsync();

                var responses =
                    users.Select(u => new UserSummaryDTO
                    (
                        id: u.Id,
                        userName: u.UserName,
                        nickname: u.Nickname
                    ));

                return Ok(responses);

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetUser(Guid id)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id, default);

                if (user == null)
                    return NotFound(new { error = "Not found user." });

                var userDto = new UserResponseDTO(
                    id: user.Id,
                    userName: user.UserName,
                    nickname: user.Nickname,
                    createdAt: user.CreatedAt,
                    lastConnected: user.LastConnected
                    );

                return Ok(userDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDTO dto)
        {
            if (string.IsNullOrEmpty(dto.userName))
                return BadRequest(new { error = "UserName is necessary." });

            if (string.IsNullOrEmpty(dto.password))
                return BadRequest(new { error = "Password is necessary." });

            Guid guid = Guid.NewGuid();

            var user = new User
            {
                Id = guid,
                UserName = dto.userName,
                Password = dto.password,
                Nickname = null, // 임시. 수정해야함
                CreatedAt = DateTime.UtcNow,
                LastConnected = DateTime.UtcNow,
            };

            try
            {
                var users = await _userRepository.GetAllAsync();
                var exist = users.Any(u => u.UserName.Equals(dto.userName));

                if (exist)
                {
                    return Conflict(new { error = "Already registered username." });
                }
                else
                {
                    await _userRepository.InsertAsync(user, default);
                    await _userRepository.SaveAsync(default);

                    var response = new UserResponseDTO(
                            id: user.Id,
                            userName: user.UserName,
                            nickname: user.Nickname,
                            createdAt: user.CreatedAt,
                            lastConnected: user.LastConnected
                        );

                    return CreatedAtAction(nameof(GetUser), new { id = user.Id }, response);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        [HttpPatch("{id:guid}/nickname")]
        public async Task<IActionResult> UpdateNickname(Guid id, [FromBody] UpdateNicknameDTO dto)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id, default);

                if (user == null)
                    return NotFound(new { error = "Not found user." });

                var users = await _userRepository.GetAllAsync();
                var exist = users.Any(u => u.Nickname.Equals(dto.nickname, StringComparison.OrdinalIgnoreCase));

                if (exist)
                {
                    return Ok(new { isExist = exist, message = "Nickname already exist." });
                }
                else
                {
                    user.Nickname = dto.nickname;
                    await _userRepository.UpdateAsync(user, default);
                    await _userRepository.SaveAsync(default);

                    return Ok(new { isExist = exist, message = "Updated nickname.", newNickname = user.Nickname });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        [HttpPatch("{id:guid}/lastconnected")]
        public async Task<IActionResult> UpdateLastConnected(Guid id)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id, default);

                if (user == null)
                    return NotFound(new { error = "Not found user." });

                user.LastConnected = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user, default);
                await _userRepository.SaveAsync(default);

                return Ok(new { message = "Updated last connected.", lastConnected = user.LastConnected });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteUser(Guid id, [FromBody]DeleteUserDTO dto)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id, default);

                if (user == null)
                    return NotFound(new { error = "Not found user." });

                if (!user.UserName.Equals(dto.userName))
                    return Unauthorized();

                if (!user.Password.Equals(dto.password))
                    return Unauthorized();

                await _userRepository.DeleteAsync(id, default);
                await _userRepository.SaveAsync(default);

                return Ok(new { message = "Deleted user.", id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }
    }
    #region DTO
    public record UserResponseDTO(Guid id, string userName, string? nickname, DateTime createdAt, DateTime? lastConnected);
    public record UserSummaryDTO(Guid id, string userName, string? nickname);
    public record CreateUserDTO(string userName, string password);
    public record UpdateNicknameDTO(string nickname);
    public record DeleteUserDTO(string userName, string password);
    #endregion
}
