/*
 * REST Api
 * (Representational State Transfer) : 특정 자원의 상태를 전송하는 API
 * 특정 자원에대한 CRUD 를 POST, GET, PUT, DELETE , PATCH 등을 통해 수행함.
 */
using Game.Persistence.Jwt;
using Game.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;
using Persistence.Repositories;
using System.Threading.Tasks;

namespace Authentication.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        public AuthController(IUserRepository userRepository, IUserSessionRepository sessions)
        {
            _userRepository = userRepository;
            _sessions = sessions;
        }

        private readonly IUserRepository _userRepository;
        private readonly IUserSessionRepository _sessions;


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            var user = await _userRepository.GetByUsernameAsync(dto.id, default);

            if (user == null)
                return Unauthorized();

            if (!user.Password.Equals(dto.pw))
                return Unauthorized();

            string sessionId = Guid.NewGuid().ToString();
            DateTime createdAt = DateTime.UtcNow;
            _sessions.Create(sessionId, new UserInfo { Id = dto.id, CreatedAt = createdAt });
            var jwt = JwtUtils.Generate(dto.id, sessionId, TimeSpan.FromHours(1));
            return Ok(new { jwt, sessionId, createdAt });
        }

        [HttpPost("logout")]
        public IActionResult Logout([FromBody] LogoutDTO dto)
        {
            if (_sessions.Get(dto.sessionId) != null)
            {
                _sessions.Remove(dto.sessionId);
                return Ok();
            }

            return NotFound();
        }

        [HttpPost("validate")]
        public IActionResult Validate([FromBody] ValidateDTO dto)
        {
            var result = JwtUtils.Validate(dto.jwt);

            foreach (var claim in result.Claims)
            {
                if (claim.Type == "session_id")
                {
                    UserInfo userInfo = _sessions.Get(claim.Value);

                    if (userInfo != null)
                    {
                        return Ok(new
                        {
                            isValid = true,
                            sessionId = claim.Value,
                            createdAt = userInfo.CreatedAt,
                            id = userInfo.Id,
                        });
                    }
                }
            }

            return Unauthorized();
        }
    }

    /// <summary>
    /// DTO(Data Transfer Object) 데이터 전송시 사용하는 객체
    /// 모든 프로젝트들의 Data Model 을 통일하려고하면
    /// 해당 Data Model 을 공통으로 사용하기위해서 공용 라이브러리를 빌드해야하고, 
    /// 그 라이브러리를 모든 프로젝트에 또 주입하는것이 프로젝트가 커질수록 번거로워짐. 
    /// 특정 계층에서는 데이터 일부만 취급해도 된다고 해도 Data Model 에 의존하면 반드시 필요없는 데이터도 다 취급해야함. 
    /// 
    /// DTO 는 프로젝트(계층) 단위로 정의하여 사용하기때문에 이런 번거로움이없어짐. 
    /// 물론 프로젝트마다 데이터구성이 완전히 똑같다는보장이 없으므로 사용시 유의해야함.
    /// </summary>
    public record LoginDTO(string id, string pw);
    public record LogoutDTO(string sessionId);
    public record ValidateDTO(string jwt);
}
