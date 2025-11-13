namespace COMP306_Group15_OpenLectureAPI.DTOs
{
    public class ReactionDtos
    {
        public record ReactionReadDto(
            string ReactionId,
            string VideoId,
            string UserId,
            string Type,
            DateTime UpdatedAt
            );

        public record ReactionUpsertDto(
            string VideoId, 
            string UserId, 
            string Type // Like | Dislike | None
            ); 

        public record ReactionPatchDto(string? Type);

        public record ReactionSummaryDto(
            string VideoId, 
            int Likes, 
            int Dislikes
            );
    }
}
