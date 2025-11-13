namespace COMP306_Group15_OpenLectureAPI.DTOs
{
    public class UploadDtos
    {
        // Step 1: Request presigned PUT URL for raw upload
        public record InitUploadDto(string UploaderId, string ContentType, string Extension);

        // Step 2: Finalize after successful upload (save metadata)
        public record FinalizeUploadDto(
            string VideoId,
            string Title,
            string Description,
            string Subject,
            string CourseCode,
            List<string> Tags,
            string UploaderId,
            string S3Bucket,
            string S3Key,
            string ContentType,
            long? SizeBytes
        );
    }
}

