using AutoMapper;
using COMP306_Group15_OpenLectureAPI.Models.COMP306_Group15_OpenLectureAPI.Models;
using global::COMP306_Group15_OpenLectureAPI.Data;
using global::COMP306_Group15_OpenLectureAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static COMP306_Group15_OpenLectureAPI.DTOs.CommentDtos;

namespace COMP306_Group15_OpenLectureAPI.Controllers
{ 
    [ApiController, Route("api/comments")]
    public class CommentsController : ControllerBase
    {
        private readonly IDynamoRepo<CommentItem> _comments;
        private readonly IDynamoRepo<Models.COMP306_Group15_OpenLectureAPI.Models.VideoItem> _videos;
        private readonly IMapper _map;

        public CommentsController(IDynamoRepo<CommentItem> comments, IDynamoRepo<VideoItem> videos, IMapper map)
        { _comments = comments; _videos = videos; _map = map; }

        [HttpGet] // grading helper
        public async Task<ActionResult<IEnumerable<CommentReadDto>>> GetAll() =>
            Ok((await _comments.GetAllAsync()).Select(_map.Map<CommentReadDto>));

        [HttpGet("{id}")]
        public async Task<ActionResult<CommentReadDto>> GetById(string id)
        {
            var e = await _comments.GetByIdAsync(id);
            return e is null ? NotFound() : Ok(_map.Map<CommentReadDto>(e));
        }

        [HttpGet("videos/{videoId}")]
        public async Task<ActionResult<IEnumerable<CommentReadDto>>> GetByVideo(string videoId, [FromQuery] bool includeDeleted = false)
        {
            var video = await _videos.GetByIdAsync(videoId); if (video is null) return NotFound("Video not found.");
            var uploaderId = video.UploaderId;

            var all = await _comments.GetAllAsync();
            var list = all.Where(c => c.VideoId == videoId && (includeDeleted || !c.IsDeleted))
                          .OrderBy(c => c.CreatedAt)
                          .Select(c => {
                              var dto = _map.Map<CommentReadDto>(c);
                              return dto with { IsOwnerReply = (c.UserId == uploaderId) };
                          });

            return Ok(list);
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<CommentReadDto>> Create(CommentCreateDto dto)
        {
            // Only the logged-in user can create for themselves
            if (JwtHelper.UserId(User) != dto.UserId && !JwtHelper.IsAdmin(User)) return Forbid();
            var e = _map.Map<CommentItem>(dto);
            await _comments.CreateAsync(e);
            return CreatedAtAction(nameof(GetById), new { id = e.CommentId }, _map.Map<CommentReadDto>(e));
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> Put(string id, CommentUpdateDto dto)
        {
            var e = await _comments.GetByIdAsync(id); if (e is null) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != e.UserId) return Forbid();
            e.Content = dto.Content; e.UpdatedAt = DateTime.UtcNow;
            await _comments.UpdateAsync(e); return NoContent();
        }

        [Authorize]
        [HttpPatch("{id}")]
        public async Task<ActionResult> Patch(string id, CommentPatchDto dto)
        {
            var e = await _comments.GetByIdAsync(id); if (e is null) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != e.UserId) return Forbid();
            if (dto.Content is not null) e.Content = dto.Content;
            if (dto.IsDeleted.HasValue) e.IsDeleted = dto.IsDeleted.Value;
            e.UpdatedAt = DateTime.UtcNow;
            await _comments.UpdateAsync(e); return NoContent();
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            var e = await _comments.GetByIdAsync(id); if (e is null) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != e.UserId) return Forbid();
            // keep hard delete for the “six methods” demo; real world might soft delete only
            await _comments.DeleteAsync(id);
            return NoContent();
        }
    }

}
