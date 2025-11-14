using System;
using System.Collections.Generic;

namespace COMP306_Group15_OpenLectureAPI.DTOs
{
    public class VideoDtos
    {
        // Read model returned to clients
        public record VideoReadDto(
            string VideoId,
            string Title,
            string Description,
            string Subject,
            string CourseCode,
            List<string> Tags,
            string UploaderId,
            DateTime UploadDate,
            DateTime? UpdatedAt,
            string Visibility,
            bool IsDeleted,
            string S3Bucket,
            string S3Key,
            string ContentType,
            long? SizeBytes,
            int ViewCount,
            int LikeCount,
            int DislikeCount,
            int CommentCount
        );

        // Create model 
        public record VideoCreateDto(
            string Title,
            string Description,
            string Subject,
            string CourseCode,
            List<string> Tags,
            string UploaderId,
            string Visibility,
            string S3Bucket,
            string S3Key,
            string ContentType,
            long? SizeBytes
        );

        // Full update (PUT) — includes S3 fields to allow replacing the video
        public record VideoUpdateDto(
            string Title,
            string Description,
            string Subject,
            string CourseCode,
            List<string> Tags,
            string Visibility,
            string S3Bucket,
            string S3Key,
            string ContentType,
            long? SizeBytes
        );

        // Partial update (PATCH)
        public record VideoPatchDto(
            string? Title,
            string? Description,
            string? Subject,
            string? CourseCode,
            List<string>? Tags,
            string? Visibility,
            bool? IsDeleted,
            string? S3Bucket,
            string? S3Key,
            string? ContentType,
            long? SizeBytes
        );
    }
}
