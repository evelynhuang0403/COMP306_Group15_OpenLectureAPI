using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using COMP306_Group15_OpenLectureAPI.Data;
using COMP306_Group15_OpenLectureAPI.Models;
using static COMP306_Group15_OpenLectureAPI.DTOs.UserDtos;

namespace COMP306_Group15_OpenLectureAPI.Controllers
{
    [ApiController, Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IDynamoRepo<UserItem> _repo;
        private readonly IMapper _map;
        public UsersController(IDynamoRepo<UserItem> repo, IMapper map) { _repo = repo; _map = map; }

        // Only Admin: Get all non-deleted users
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserReadDto>>> GetAll() =>
            Ok((await _repo.GetAllAsync()).Where(u => !u.IsDeleted).Select(_map.Map<UserReadDto>));

        // GET /api/users/{id} — Owner or Admin
        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<UserReadDto>> GetById(string id)
        {
            var e = await _repo.GetByIdAsync(id);
            if (e is null || e.IsDeleted) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != id) return Forbid();
            return Ok(_map.Map<UserReadDto>(e));
        }

        // Self profile: GET /api/users/me — Auth required (Student or Admin)
        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UserReadDto>> Me()
        {
            var id = JwtHelper.UserId(User); if (id is null) return Unauthorized();
            var e = await _repo.GetByIdAsync(id);
            return e is null || e.IsDeleted ? NotFound() : Ok(_map.Map<UserReadDto>(e));
        }

        // POST /api/users — Admin only (create user, normal users use /auth/register)
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<UserReadDto>> Create(UserCreateDto dto)
        {
            var e = _map.Map<UserItem>(dto);
            e.EmailNormalized = e.Email?.Trim().ToLowerInvariant() ?? e.EmailNormalized;
            await _repo.CreateAsync(e);
            return CreatedAtAction(nameof(GetById), new { id = e.UserId }, _map.Map<UserReadDto>(e));
        }

        // PUT /api/users/{id} — Admin only (full replace)
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<ActionResult> Put(string id, UserUpdateDto dto)
        {
            var e = await _repo.GetByIdAsync(id); if (e is null || e.IsDeleted) return NotFound();
            _map.Map(dto, e);
            e.EmailNormalized = e.Email?.Trim().ToLowerInvariant() ?? e.EmailNormalized;
            await _repo.UpdateAsync(e);
            return NoContent();
        }

        // Partial update: PATCH /api/users/{id} — Owner or Admin
        [Authorize]
        [HttpPatch("{id}")]
        public async Task<ActionResult> Patch(string id, UserPatchDto dto)
        {
            var e = await _repo.GetByIdAsync(id); if (e is null) return NotFound();

            var isAdmin = JwtHelper.IsAdmin(User);
            var isOwner = JwtHelper.UserId(User) == id;
            if (!isAdmin && !isOwner) return Forbid();

            // Owner-editable
            if (dto.FullName is not null) e.FullName = dto.FullName;
            if (dto.Email is not null)
            {
                e.Email = dto.Email;
                e.EmailNormalized = dto.Email.Trim().ToLowerInvariant();
            }

            // Admin-only fields
            if (isAdmin && dto.Role is not null) e.Role = dto.Role;
            if (isAdmin && dto.IsDeleted.HasValue) e.IsDeleted = dto.IsDeleted.Value;

            await _repo.UpdateAsync(e);
            return NoContent();
        }

        // PATCH /api/users/me — Auth required (Owner-only fields)
        [Authorize]
        [HttpPatch("me")]
        public Task<ActionResult> PatchMe([FromBody] UserPatchDto dto)
        {
            var id = JwtHelper.UserId(User);
            if (id is null) return Task.FromResult<ActionResult>(Unauthorized());
            return Patch(id, dto);
        }

        // Soft delete: DELETE /api/users/{id} — Owner or Admin 
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            var e = await _repo.GetByIdAsync(id); if (e is null) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != id) return Forbid();
            e.IsDeleted = true;
            await _repo.UpdateAsync(e);
            return NoContent();
        }
    }
}
