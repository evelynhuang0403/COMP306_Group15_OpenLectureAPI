using Amazon.DynamoDBv2.DataModel;

namespace COMP306_Group15_OpenLectureAPI.Models
{
    [DynamoDBTable("OpenLecture_Comments")]
    public class CommentItem
    {
        [DynamoDBHashKey] public string CommentId { get; set; } = default!;
        public string VideoId { get; set; } = default!;
        public string UserId { get; set; } = default!;
        public string Content { get; set; } = default!;
        public string? ParentId { get; set; } // null = top-level, else reply
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
