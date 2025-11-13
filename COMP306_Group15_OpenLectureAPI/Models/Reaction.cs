namespace COMP306_Group15_OpenLectureAPI.Models
{
    using Amazon.DynamoDBv2.DataModel;

    [DynamoDBTable("OpenLecture_Reactions")]
    public class ReactionItem
    {
        // Composite “primary key” packed into the hash key to enforce one reaction per user per video
        [DynamoDBHashKey] public string ReactionId { get; set; } = default!; // "{VideoId}|{UserId}" 
        public string VideoId { get; set; } = default!;
        public string UserId { get; set; } = default!;
        public string Type { get; set; } = "None"; // Like | Dislike | None
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

}
