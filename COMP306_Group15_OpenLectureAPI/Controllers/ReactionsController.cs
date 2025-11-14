using AutoMapper;
using COMP306_Group15_OpenLectureAPI.Data;
using COMP306_Group15_OpenLectureAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static COMP306_Group15_OpenLectureAPI.DTOs.ReactionDtos;

namespace COMP306_Group15_OpenLectureAPI.Controllers
{
    [ApiController]
    [Route("api/reactions")]
    public class ReactionsController : ControllerBase
    {
        private readonly IDynamoRepo<ReactionItem> _reactions;
        private readonly IDynamoRepo<VideoItem> _videos;
        private readonly IMapper _map;

        public ReactionsController(IDynamoRepo<ReactionItem> reactions, IDynamoRepo<VideoItem> videos, IMapper map)
        {
            _reactions = reactions; _videos = videos; _map = map;
        }


        // Get all: GET /api/reactions — Public 
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReactionReadDto>>> GetAll() =>
            Ok((await _reactions.GetAllAsync()).Select(_map.Map<ReactionReadDto>));

        // Get one: GET /api/reactions/{id} — Public
        // id format = "{videoId}|{userId}"
        [HttpGet("{id}")]
        public async Task<ActionResult<ReactionReadDto>> GetById(string id)
        {
            var e = await _reactions.GetByIdAsync(id);
            return e is null ? NotFound() : Ok(_map.Map<ReactionReadDto>(e));
        }

        // Upsert: POST /api/reactions — Auth required (Student or Admin)
        // Enforces one reaction per user per video via composite id "{videoId}|{userId}"
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<ReactionReadDto>> Post(ReactionUpsertDto dto)
        {
            if (!IsValidType(dto.Type)) return BadRequest("Type must be Like, Dislike, or None.");
            if (JwtHelper.UserId(User) != dto.UserId && !JwtHelper.IsAdmin(User)) return Forbid();

            var id = $"{dto.VideoId}|{dto.UserId}";
            var e = await _reactions.GetByIdAsync(id) ?? new ReactionItem
            {
                ReactionId = id,
                VideoId = dto.VideoId,
                UserId = dto.UserId
            };

            e.Type = dto.Type;
            e.UpdatedAt = DateTime.UtcNow;

            // Upsert (overwrite if exists; create if not)
            await _reactions.UpdateAsync(e);

            await UpdateVideoCounters(dto.VideoId);
            return CreatedAtAction(nameof(GetById), new { id = e.ReactionId }, _map.Map<ReactionReadDto>(e));
        }

        // Replace: PUT /api/reactions/{id} — Auth required (Owner or Admin)
        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> Put(string id, ReactionUpsertDto dto)
        {
            if (!IsValidType(dto.Type)) return BadRequest("Type must be Like, Dislike, or None.");
            if ($"{dto.VideoId}|{dto.UserId}" != id) return BadRequest("Id mismatch.");
            if (JwtHelper.UserId(User) != dto.UserId && !JwtHelper.IsAdmin(User)) return Forbid();

            var e = await _reactions.GetByIdAsync(id) ?? new ReactionItem
            {
                ReactionId = id,
                VideoId = dto.VideoId,
                UserId = dto.UserId
            };

            e.Type = dto.Type;
            e.UpdatedAt = DateTime.UtcNow;

            await _reactions.UpdateAsync(e);
            await UpdateVideoCounters(dto.VideoId);
            return NoContent();
        }

        // Partial update: PATCH /api/reactions/{id} — Auth required (Owner or Admin)
        // Allows changing only the reaction type
        [Authorize]
        [HttpPatch("{id}")]
        public async Task<ActionResult> Patch(string id, ReactionPatchDto dto)
        {
            var e = await _reactions.GetByIdAsync(id);
            if (e is null) return NotFound();

            if (JwtHelper.UserId(User) != e.UserId && !JwtHelper.IsAdmin(User)) return Forbid();

            if (dto.Type is not null)
            {
                if (!IsValidType(dto.Type)) return BadRequest("Type must be Like, Dislike, or None.");
                e.Type = dto.Type;
            }

            e.UpdatedAt = DateTime.UtcNow;
            await _reactions.UpdateAsync(e);
            await UpdateVideoCounters(e.VideoId);
            return NoContent();
        }

        // Delete: DELETE /api/reactions/{id} — Auth required (Owner or Admin)
        // We DON'T use this method in the app, it's just for meeting the rubric requirements
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            var e = await _reactions.GetByIdAsync(id);
            if (e is null) return NoContent();

            if (JwtHelper.UserId(User) != e.UserId && !JwtHelper.IsAdmin(User)) return Forbid();

            await _reactions.DeleteAsync(id);
            await UpdateVideoCounters(e.VideoId);
            return NoContent();
        }

        // Summary: GET /api/reactions/videos/{videoId}/summary — Public
        // Auth: None
        // Returns total likes and dislikes for a video
        [HttpGet("videos/{videoId}/summary")]
        public async Task<ActionResult<ReactionSummaryDto>> Summary(string videoId)
        {
            var all = await _reactions.GetAllAsync();
            var likes = all.Count(r => r.VideoId == videoId && r.Type == "Like");
            var dislikes = all.Count(r => r.VideoId == videoId && r.Type == "Dislike");
            return Ok(new ReactionSummaryDto(videoId, likes, dislikes));
        }

        private async Task UpdateVideoCounters(string videoId)
        {
            var v = await _videos.GetByIdAsync(videoId);
            if (v is null) return;

            var all = await _reactions.GetAllAsync();
            v.LikeCount = all.Count(r => r.VideoId == videoId && r.Type == "Like");
            v.DislikeCount = all.Count(r => r.VideoId == videoId && r.Type == "Dislike");
            await _videos.UpdateAsync(v);
        }

        private static bool IsValidType(string type) =>
            string.Equals(type, "Like", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "Dislike", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "None", StringComparison.OrdinalIgnoreCase);
    }
}
