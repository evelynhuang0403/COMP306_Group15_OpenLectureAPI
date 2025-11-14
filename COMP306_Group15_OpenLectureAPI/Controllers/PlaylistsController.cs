using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AutoMapper;
using COMP306_Group15_OpenLectureAPI.Data;
using COMP306_Group15_OpenLectureAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static COMP306_Group15_OpenLectureAPI.DTOs.PlaylistDtos;

namespace COMP306_Group15_OpenLectureAPI.Controllers
{
    [ApiController]
    [Route("api/playlists")]
    public class PlaylistsController : ControllerBase
    {
        private readonly IDynamoRepo<PlaylistItem> _repo;
        private readonly IMapper _map;
        private readonly IAmazonDynamoDB _ddb;
        private readonly IConfiguration _cfg;

        public PlaylistsController(
            IDynamoRepo<PlaylistItem> repo,
            IMapper map,
            IAmazonDynamoDB ddb,
            IConfiguration cfg)
        {
            _repo = repo; _map = map; _ddb = ddb; _cfg = cfg;
        }

        // List: GET /api/playlists — Public + Owner/Admin sees private
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PlaylistReadDto>>> GetAll()
        {
            var userId = JwtHelper.UserId(User);
            var list = (await _repo.GetAllAsync()).Where(p => !p.IsDeleted)
                      .Where(p => p.Visibility == "Public" || (userId != null && p.OwnerId == userId) || JwtHelper.IsAdmin(User));
            return Ok(list.Select(_map.Map<PlaylistReadDto>));
        }

        // Get one: GET /api/playlists/{id} — Public or Owner/Admin if private
        [HttpGet("{id}")]
        public async Task<ActionResult<PlaylistReadDto>> GetById(string id)
        {
            var userId = JwtHelper.UserId(User);
            var p = await _repo.GetByIdAsync(id);
            if (p is null || p.IsDeleted) return NotFound();
            if (p.Visibility != "Public" && p.OwnerId != userId && !JwtHelper.IsAdmin(User)) return Forbid();
            return Ok(_map.Map<PlaylistReadDto>(p));
        }

        // Create: POST /api/playlists — Auth required
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<PlaylistReadDto>> Create(PlaylistCreateDto dto)
        {
            if (JwtHelper.UserId(User) != dto.OwnerId && !JwtHelper.IsAdmin(User)) return Forbid();
            var e = _map.Map<PlaylistItem>(dto);
            e.PlaylistId = $"pl_{Guid.NewGuid():N}";
            e.VideoIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase); // if no videos provided, init empty set
            await _repo.CreateAsync(e);
            return CreatedAtAction(nameof(GetById), new { id = e.PlaylistId }, _map.Map<PlaylistReadDto>(e));
        }

        // Replace: PUT /api/playlists/{id} — Owner or Admin
        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> Put(string id, PlaylistUpdateDto dto)
        {
            var e = await _repo.GetByIdAsync(id); if (e is null || e.IsDeleted) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != e.OwnerId) return Forbid();

            _map.Map(dto, e);
            e.VideoIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await _repo.UpdateAsync(e);
            return NoContent();
        }

        // Partial update: PATCH /api/playlists/{id} — Owner or Admin
        [Authorize]
        [HttpPatch("{id}")]
        public async Task<ActionResult> Patch(string id, PlaylistPatchDto dto)
        {
            var e = await _repo.GetByIdAsync(id); if (e is null) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != e.OwnerId) return Forbid();

            if (dto.Name is not null) e.Name = dto.Name;
            if (dto.Visibility is not null) e.Visibility = dto.Visibility;
            await _repo.UpdateAsync(e);
            return NoContent();
        }

        // Delete (soft): DELETE /api/playlists/{id} — Owner or Admin
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            var e = await _repo.GetByIdAsync(id); if (e is null) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != e.OwnerId) return Forbid();
            e.IsDeleted = true; await _repo.UpdateAsync(e); return NoContent();
        }

        // List my playlists only: GET /api/playlists/me — Auth required
        // Returns ALL of the caller's playlists (Public and Private), not deleted
        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<IEnumerable<PlaylistReadDto>>> GetMine()
        {
            var me = JwtHelper.UserId(User);
            if (me is null) return Unauthorized();

            var list = (await _repo.GetAllAsync())
                .Where(p => !p.IsDeleted && p.OwnerId == me);

            return Ok(list.Select(_map.Map<PlaylistReadDto>));
        }

        // Add video: POST /api/playlists/{id}/videos — Owner or Admin
        // Appends a video id (case-insensitive). Rewrites the set to avoid in-place issues.
        [Authorize]
        [HttpPost("{id}/videos")]
        public async Task<ActionResult> AddVideo(string id, PlaylistVideoChangeDto dto)
        {
            var e = await _repo.GetByIdAsync(id); if (e is null) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != e.OwnerId) return Forbid();

            var vid = dto.VideoId?.Trim();
            if (string.IsNullOrEmpty(vid)) return BadRequest("VideoId is required.");

            e.VideoIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            e.VideoIds.Add(vid); // HashSet de-dupes
            await _repo.UpdateAsync(e);
            return NoContent();
        }

        // Remove video: DELETE /api/playlists/{id}/videos/{videoId} — Owner or Admin
        // Handles empty-set case by removing the attribute (DynamoDB disallows empty sets)
        [Authorize]
        [HttpDelete("{id}/videos/{videoId}")]
        public async Task<ActionResult> RemoveVideo(string id, string videoId)
        {
            var e = await _repo.GetByIdAsync(id); if (e is null) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != e.OwnerId) return Forbid();

            if (e.VideoIds is null || e.VideoIds.Count == 0) return NoContent();

            var before = e.VideoIds.Count;
            e.VideoIds.RemoveWhere(v => string.Equals(v, videoId, StringComparison.OrdinalIgnoreCase));

            if (e.VideoIds.Count == before) return NoContent(); // nothing changed

            if (e.VideoIds.Count > 0)
            {
                await _repo.UpdateAsync(e);
            }
            else
            {
                // Remove the attribute entirely so we don't write an empty set
                var table = _cfg["DynamoDb:PlaylistsTable"] ?? "OpenLecture_Playlists";
                var key = new Dictionary<string, AttributeValue> { { "PlaylistId", new AttributeValue { S = id } } };

                await _ddb.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = table,
                    Key = key,
                    UpdateExpression = "REMOVE VideoIds"
                });
            }

            return NoContent();
        }
    }
}
