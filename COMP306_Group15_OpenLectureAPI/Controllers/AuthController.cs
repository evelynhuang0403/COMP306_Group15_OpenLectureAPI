using COMP306_Group15_OpenLectureAPI.Data;
using COMP306_Group15_OpenLectureAPI.Models;
using Microsoft.AspNetCore.Mvc;
using static COMP306_Group15_OpenLectureAPI.DTOs.AuthDtos;
using BC = BCrypt.Net.BCrypt;

namespace COMP306_Group15_OpenLectureAPI.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly IDynamoRepo<UserItem> _users;
        private readonly IConfiguration _cfg;

        public AuthController(IDynamoRepo<UserItem> users, IConfiguration cfg)
        {
            _users = users; _cfg = cfg;
        }

        // POST auth/register  -> create a new Student user
        [HttpPost("register")]
        public async Task<ActionResult<RegisterResponseDto>> Register(RegisterRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Email and password are required.");

            var normalized = dto.Email.Trim().ToLowerInvariant();

            // Reject duplicate e-mail (scan ok for demo scale)
            var all = await _users.GetAllAsync();
            if (all.Any(u => u.EmailNormalized == normalized && !u.IsDeleted))
                return Conflict("Email already registered.");

            var user = new UserItem
            {
                UserId = $"u_{Guid.NewGuid():N}",
                Email = dto.Email.Trim(),
                EmailNormalized = normalized,
                FullName = dto.FullName?.Trim() ?? "",
                Role = "Student",
                // Hash with BCrypt alias
                PasswordHash = BC.HashPassword(dto.Password),
                PasswordUpdatedAt = DateTime.UtcNow
            };

            await _users.CreateAsync(user);

            return Created("", new RegisterResponseDto(user.UserId, user.Email, user.FullName, user.Role));
        }

        // POST auth/login  -> verify email+password, return JWT
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto dto)
        {
            var normalized = dto.Email.Trim().ToLowerInvariant();
            var list = await _users.GetAllAsync();
            var u = list.FirstOrDefault(x => x.EmailNormalized == normalized && !x.IsDeleted);
            if (u is null) return Unauthorized("Invalid email or password.");

            // Verify with BCrypt alias
            var ok = BC.Verify(dto.Password, u.PasswordHash);
            if (!ok) return Unauthorized("Invalid email or password.");

            var token = JwtHelper.IssueToken(_cfg, u);
            return Ok(new LoginResponseDto(token, u.UserId, u.FullName, u.Role));
        }
    }
}


