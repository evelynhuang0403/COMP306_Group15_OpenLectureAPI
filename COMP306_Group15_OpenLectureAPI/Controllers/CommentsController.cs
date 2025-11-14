using AutoMapper;
using COMP306_Group15_OpenLectureAPI.Data;
using COMP306_Group15_OpenLectureAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static COMP306_Group15_OpenLectureAPI.DTOs.CommentDtos;

namespace COMP306_Group15_OpenLectureAPI.Controllers
{
    [ApiController]
    [Route("api/comments")]
    public class CommentsController : ControllerBase
    {
        private readonly IDynamoRepo<CommentItem> _comments;
        private readonly IDynamoRepo<VideoItem> _videos;
        private readonly IMapper _map;

        public CommentsController(
            IDynamoRepo<CommentItem> comments,
            IDynamoRepo<VideoItem> videos,
            IMapper map)
        {
            _comments = comments;
            _videos = videos;
            _map = map;
        }

        // List all comments: GET /api/comments — Public
        // Only for rubric requirements. Real apps should query by video.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CommentReadDto>>> GetAll()
        {
            var allComments = await _comments.GetAllAsync();
            var videos = await _videos.GetAllAsync();
            var uploaderByVideoId = videos.ToDictionary(v => v.VideoId, v => v.UploaderId, StringComparer.Ordinal);

            var result = allComments
                .Select(c =>
                {
                    var dto = _map.Map<CommentReadDto>(c);
                    if (uploaderByVideoId.TryGetValue(c.VideoId, out var uploaderId))
                        dto = dto with { IsOwnerReply = (c.UserId == uploaderId) };
                    return dto;
                });

            return Ok(result);
        }

        // Get one comment: GET /api/comments/{id} — Public
        [HttpGet("{id}")]
        public async Task<ActionResult<CommentReadDto>> GetById(string id)
        {
            var e = await _comments.GetByIdAsync(id);
            if (e is null) return NotFound();

            var dto = _map.Map<CommentReadDto>(e);

            // Look up the video to determine owner
            var video = await _videos.GetByIdAsync(e.VideoId);
            if (video is not null)
                dto = dto with { IsOwnerReply = (e.UserId == video.UploaderId) };

            return Ok(dto);
        }

        // List comments for a video: GET /api/comments/videos/{videoId} — Public
        // Query:
        //   ?includeDeleted=true  (default false)
        // Usage:
        //   - Returns comments sorted by CreatedAt ascending.
        //   - Flags uploader replies (IsOwnerReply = true) for UI highlighting.
        [HttpGet("videos/{videoId}")]
        public async Task<ActionResult<IEnumerable<CommentReadDto>>> GetByVideo(
            string videoId,
            [FromQuery] bool includeDeleted = false)
        {
            // Validate video exists so we can also compute uploader for IsOwnerReply
            var video = await _videos.GetByIdAsync(videoId);
            if (video is null) return NotFound("Video not found.");

            var uploaderId = video.UploaderId;

            var all = await _comments.GetAllAsync();
            var list = all.Where(c => c.VideoId == videoId && (includeDeleted || !c.IsDeleted))
                          .OrderBy(c => c.CreatedAt)
                          .Select(c =>
                          {
                              var dto = _map.Map<CommentReadDto>(c);
                              return dto with { IsOwnerReply = (c.UserId == uploaderId) };
                          });

            return Ok(list);
        }

        // Create: POST /api/comments — Auth required (Student or Admin)
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<CommentReadDto>> Create(CommentCreateDto dto)
        {
            // Only the logged-in user can create for themselves (admins allowed)
            if (JwtHelper.UserId(User) != dto.UserId && !JwtHelper.IsAdmin(User)) return Forbid();

            // Optional safety: ensure the target video exists
            var video = await _videos.GetByIdAsync(dto.VideoId);
            if (video is null) return NotFound("Target video not found.");

            var e = _map.Map<CommentItem>(dto);
            e.CommentId = $"c_{Guid.NewGuid():N}";
            e.CreatedAt = DateTime.UtcNow;
            e.UpdatedAt = null;
            e.IsDeleted = false;

            await _comments.CreateAsync(e);

            // keep denormalized counter in sync
            await UpdateVideoCommentCount(dto.VideoId);

            return CreatedAtAction(nameof(GetById), new { id = e.CommentId }, _map.Map<CommentReadDto>(e));
        }

        // Replace: PUT /api/comments/{id} — Auth required (Owner or Admin)
        // Replaces full content (we only expose updatable field here).
        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> Put(string id, CommentUpdateDto dto)
        {
            var e = await _comments.GetByIdAsync(id);
            if (e is null) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != e.UserId) return Forbid();

            e.Content = dto.Content;
            e.UpdatedAt = DateTime.UtcNow;

            await _comments.UpdateAsync(e);
            // content change does NOT affect count
            return NoContent();
        }

        // Partial update: PATCH /api/comments/{id} — Auth required (Owner or Admin)
        // Allows changing content and soft-delete flag.
        [Authorize]
        [HttpPatch("{id}")]
        public async Task<ActionResult> Patch(string id, CommentPatchDto dto)
        {
            var e = await _comments.GetByIdAsync(id);
            if (e is null) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != e.UserId) return Forbid();

            if (dto.Content is not null) e.Content = dto.Content;

            var shouldRecount = false;
            if (dto.IsDeleted.HasValue)
            {
                e.IsDeleted = dto.IsDeleted.Value;
                shouldRecount = true;
            }

            e.UpdatedAt = DateTime.UtcNow;
            await _comments.UpdateAsync(e);

            if (shouldRecount)
                await UpdateVideoCommentCount(e.VideoId);

            return NoContent();
        }

        // Delete (soft): DELETE /api/comments/{id} — Auth required (Owner or Admin)
        // Sets IsDeleted = true and updates UpdatedAt.
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            var e = await _comments.GetByIdAsync(id);
            if (e is null) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != e.UserId) return Forbid();

            e.IsDeleted = true;
            e.UpdatedAt = DateTime.UtcNow;

            await _comments.UpdateAsync(e);

            // keep denormalized counter in sync
            await UpdateVideoCommentCount(e.VideoId);

            return NoContent();
        }

        // List replies for a specific parent comment (thread): GET /api/comments/parents/{parentId}/replies — Public
        // Query:
        //   ?includeDeleted=true  (default false)
        // Behavior:
        //   - Sorts by CreatedAt ascending for consistent UI threading.
        [HttpGet("parents/{parentId}/replies")]
        public async Task<ActionResult<IEnumerable<CommentReadDto>>> GetReplies(
            string parentId,
            [FromQuery] bool includeDeleted = false)
        {
            // Ensure parent exists
            var parent = await _comments.GetByIdAsync(parentId);
            if (parent is null) return NotFound("Parent comment not found.");

            // Load the video to identify its uploader and set IsOwnerReply
            var video = await _videos.GetByIdAsync(parent.VideoId);
            if (video is null) return NotFound("Video for parent comment not found.");
            var uploaderId = video.UploaderId;

            var all = await _comments.GetAllAsync();
            var replies = all
                .Where(c => c.ParentId == parentId && (includeDeleted || !c.IsDeleted))
                .OrderBy(c => c.CreatedAt)
                .Select(c =>
                {
                    var dto = _map.Map<CommentReadDto>(c);
                    return dto with { IsOwnerReply = (c.UserId == uploaderId) };
                });

            return Ok(replies);
        }

        // --- helper: recompute and persist the denormalized comment counter on the video
        private async Task UpdateVideoCommentCount(string videoId)
        {
            var video = await _videos.GetByIdAsync(videoId);
            if (video is null) return;

            var all = await _comments.GetAllAsync();
            video.CommentCount = all.Count(c => c.VideoId == videoId && !c.IsDeleted);
            await _videos.UpdateAsync(video);
        }
    }
}