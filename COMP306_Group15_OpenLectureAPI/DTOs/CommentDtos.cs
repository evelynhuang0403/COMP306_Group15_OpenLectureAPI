namespace COMP306_Group15_OpenLectureAPI.DTOs
{
    public class CommentDtos
    {
        public record CommentReadDto(
            string CommentId,
            string VideoId,
            string UserId,
            string Content,
            DateTime CreatedAt,
            DateTime? UpdatedAt,
            bool IsDeleted,
            string? ParentId,
            bool IsOwnerReply
            );

        public record CommentCreateDto(
            string CommentId,
            string VideoId,
            string UserId,
            string Content,
            string? ParentId
            );

        public record CommentUpdateDto(string Content);

        public record CommentPatchDto(
            string? Content,
            bool? IsDeleted
            );
    }
}
