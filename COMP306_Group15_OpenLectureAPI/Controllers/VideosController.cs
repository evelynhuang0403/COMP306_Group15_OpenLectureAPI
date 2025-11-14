// Controllers/VideosController.cs
using Amazon.S3;
using Amazon.S3.Model;
using AutoMapper;
using COMP306_Group15_OpenLectureAPI.Data;
using COMP306_Group15_OpenLectureAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static COMP306_Group15_OpenLectureAPI.DTOs.VideoDtos;

namespace COMP306_Group15_OpenLectureAPI.Controllers
{
    [ApiController]
    [Route("api/videos")]
    public class VideosController : ControllerBase
    {
        private readonly IDynamoRepo<VideoItem> _videos;
        private readonly IMapper _map;
        private readonly IAmazonS3 _s3;

        public VideosController(IDynamoRepo<VideoItem> videos, IMapper map, IAmazonS3 s3)
        {
            _videos = videos; _map = map; _s3 = s3;
        }

        // List videos: GET /api/videos — Public + Owner/Admin sees private
        // Auth: Optional (required only to see your private videos)
        // Query:
        //   ?uploader=USERID
        //   &subject=Computer%20Science
        //   &courseCode=COMP306
        //   &tag=jwt&tag=rest      (repeatable) OR &tags=jwt,rest
        //   &q=keyword             (matches title/description)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<VideoReadDto>>> GetAll(
            [FromQuery] string? uploader = null,
            [FromQuery] string? subject = null,
            [FromQuery] string? courseCode = null,
            [FromQuery] string[]? tag = null,
            [FromQuery(Name = "tags")] string? tagsCsv = null,
            [FromQuery] string? q = null)
        {
            var userId = JwtHelper.UserId(User);
            var isAdmin = JwtHelper.IsAdmin(User);
            var list = (await _videos.GetAllAsync()).Where(v => !v.IsDeleted);

            // Visibility: everyone sees public; owner/admin also sees private
            list = list.Where(v =>
                v.Visibility.Equals("Public", StringComparison.OrdinalIgnoreCase) ||
                (userId != null && v.UploaderId == userId) ||
                isAdmin);

            if (!string.IsNullOrWhiteSpace(uploader))
                list = list.Where(v => v.UploaderId == uploader);

            if (!string.IsNullOrWhiteSpace(subject))
                list = list.Where(v => string.Equals(v.Subject, subject, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(courseCode))
                list = list.Where(v => string.Equals(v.CourseCode, courseCode, StringComparison.OrdinalIgnoreCase));

            var tags = new List<string>();
            if (tag is { Length: > 0 }) tags.AddRange(tag);
            if (!string.IsNullOrWhiteSpace(tagsCsv))
                tags.AddRange(tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (tags.Count > 0)
                list = list.Where(v => v.Tags != null && v.Tags.Any(t => tags.Contains(t, StringComparer.OrdinalIgnoreCase)));

            if (!string.IsNullOrWhiteSpace(q))
            {
                var cmp = StringComparison.OrdinalIgnoreCase;
                list = list.Where(v =>
                    (!string.IsNullOrEmpty(v.Title) && v.Title.Contains(q, cmp)) ||
                    (!string.IsNullOrEmpty(v.Description) && v.Description.Contains(q, cmp)));
            }

            return Ok(list.Select(_map.Map<VideoReadDto>));
        }

        // Get one: GET /api/videos/{id} — Public or Owner/Admin if private
        // Auth: Optional (required if private)
        [HttpGet("{id}")]
        public async Task<ActionResult<VideoReadDto>> GetById(string id)
        {
            var userId = JwtHelper.UserId(User);
            var v = await _videos.GetByIdAsync(id);
            if (v is null || v.IsDeleted) return NotFound();

            var canSee = v.Visibility.Equals("Public", StringComparison.OrdinalIgnoreCase)
                         || v.UploaderId == userId
                         || JwtHelper.IsAdmin(User);
            if (!canSee) return Forbid();

            return Ok(_map.Map<VideoReadDto>(v));
        }

        // Create: POST /api/videos — Auth required (Student or Admin)
        // Auth: Required. `UploaderId` must match caller.
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<VideoReadDto>> Create(VideoCreateDto dto)
        {
            if (!IsValidVisibility(dto.Visibility))
                return BadRequest("Visibility must be Public or Private.");
            if (!dto.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                return BadRequest("ContentType must start with video/.");

            var caller = JwtHelper.UserId(User);
            if (caller is null || caller != dto.UploaderId)
                return Forbid();

            var e = _map.Map<VideoItem>(dto);
            e.VideoId = $"v_{Guid.NewGuid():N}"; // server-generated ID
            await _videos.CreateAsync(e);

            return CreatedAtAction(nameof(GetById), new { id = e.VideoId }, _map.Map<VideoReadDto>(e));
        }

        // Replace entire record (including file): PUT /api/videos/{id} — Owner or Admin
        // Auth: Required. Allows pointing to a NEW S3 object (replace the video).
        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> Put(string id, VideoUpdateDto dto)
        {
            if (!IsValidVisibility(dto.Visibility))
                return BadRequest("Visibility must be Public or Private.");
            if (!dto.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                return BadRequest("ContentType must start with video/.");

            var v = await _videos.GetByIdAsync(id);
            if (v is null || v.IsDeleted) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != v.UploaderId) return Forbid();

            // Full metadata replacement
            v.Title = dto.Title;
            v.Description = dto.Description;
            v.Subject = dto.Subject;
            v.CourseCode = dto.CourseCode;
            v.Tags = dto.Tags ?? new List<string>();
            v.Visibility = dto.Visibility;

            // Allow replacing the underlying uploaded file by switching to a new S3 object
            v.S3Bucket = dto.S3Bucket;
            v.S3Key = dto.S3Key;
            v.ContentType = dto.ContentType;
            v.SizeBytes = dto.SizeBytes;

            v.UpdatedAt = DateTime.UtcNow;

            await _videos.UpdateAsync(v);
            return NoContent();
        }

        // Partial update: PATCH /api/videos/{id} — Owner or Admin
        // Auth: Required. Any provided S3 fields will swap the file reference.
        [Authorize]
        [HttpPatch("{id}")]
        public async Task<ActionResult> Patch(string id, VideoPatchDto dto)
        {
            var v = await _videos.GetByIdAsync(id);
            if (v is null) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != v.UploaderId) return Forbid();

            if (dto.Title is not null) v.Title = dto.Title;
            if (dto.Description is not null) v.Description = dto.Description;
            if (dto.Subject is not null) v.Subject = dto.Subject;
            if (dto.CourseCode is not null) v.CourseCode = dto.CourseCode;
            if (dto.Tags is not null) v.Tags = dto.Tags;

            if (dto.Visibility is not null)
            {
                if (!IsValidVisibility(dto.Visibility))
                    return BadRequest("Visibility must be Public or Private.");
                v.Visibility = dto.Visibility;
            }

            if (dto.IsDeleted.HasValue) v.IsDeleted = dto.IsDeleted.Value;

            // Optional file swap on PATCH
            if (dto.ContentType is not null && !dto.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                return BadRequest("ContentType must start with video/.");

            if (dto.S3Bucket is not null) v.S3Bucket = dto.S3Bucket;
            if (dto.S3Key is not null) v.S3Key = dto.S3Key;
            if (dto.ContentType is not null) v.ContentType = dto.ContentType;
            if (dto.SizeBytes.HasValue) v.SizeBytes = dto.SizeBytes;

            v.UpdatedAt = DateTime.UtcNow;

            await _videos.UpdateAsync(v);
            return NoContent();
        }

        // Delete (soft): DELETE /api/videos/{id} — Owner or Admin
        // Auth: Required
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            var v = await _videos.GetByIdAsync(id);
            if (v is null) return NotFound();
            if (!JwtHelper.IsAdmin(User) && JwtHelper.UserId(User) != v.UploaderId) return Forbid();

            v.IsDeleted = true;
            await _videos.UpdateAsync(v);
            return NoContent();
        }

        // Playback URL: GET /api/videos/{id}/url — Public or Owner/Admin if private
        // Auth: required if private
        [HttpGet("{id}/url")]
        public async Task<ActionResult> GetPlaybackUrl(string id)
        {
            var userId = JwtHelper.UserId(User);
            var v = await _videos.GetByIdAsync(id);
            if (v is null || v.IsDeleted) return NotFound();

            var canSeePrivate = v.Visibility.Equals("Public", StringComparison.OrdinalIgnoreCase)
                                || v.UploaderId == userId
                                || JwtHelper.IsAdmin(User);
            if (!canSeePrivate) return Forbid();

            var url = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = v.S3Bucket,
                Key = v.S3Key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(10)
            });

            return Ok(new { playbackUrl = url });
        }

        private static bool IsValidVisibility(string v) =>
            string.Equals(v, "Public", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "Private", StringComparison.OrdinalIgnoreCase);
    }
}
