using Amazon.S3;
using Amazon.S3.Model;
using COMP306_Group15_OpenLectureAPI.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static COMP306_Group15_OpenLectureAPI.DTOs.UploadDtos;

namespace COMP306_Group15_OpenLectureAPI.Controllers
{
    [ApiController]
    [Route("api/videos/uploads")]
    public class UploadsController : ControllerBase
    {
        private readonly IAmazonS3 _s3;
        private readonly IConfiguration _cfg;

        public UploadsController(IAmazonS3 s3, IConfiguration cfg)
        {
            _s3 = s3; _cfg = cfg;
        }

        // Request presigned URL: POST /api/videos/uploads/init — Auth required
        // Usage: client obtains PUT URL to upload raw bytes directly to S3
        [Authorize]
        [HttpPost("init")]
        public ActionResult Init(InitUploadDto dto)
        {
            if (!dto.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only video content types are allowed.");
            if (dto.Extension is not ("mp4" or "webm"))
                return BadRequest("Only mp4 and webm are allowed.");

            var bucket = _cfg["S3:Bucket"]!;
            var key = $"videos/{dto.UploaderId}/{DateTime.UtcNow:yyyy/MM}/vid_{Guid.NewGuid():N}.{dto.Extension}";

            var url = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = bucket,
                Key = key,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(15),
                ContentType = dto.ContentType
            });

            return Ok(new { bucket, key, uploadUrl = url });
        }
    }
}
