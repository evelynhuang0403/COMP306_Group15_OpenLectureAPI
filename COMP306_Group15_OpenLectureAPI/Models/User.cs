namespace COMP306_Group15_OpenLectureAPI.Models
{
    // Models/UserItem.cs
    using Amazon.DynamoDBv2.DataModel;

    [DynamoDBTable("OpenLecture_Users")]
    public class UserItem
    {
        [DynamoDBHashKey] public string UserId { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string EmailNormalized { get; set; } = default!;   // for case-insensitive lookups
        public string FullName { get; set; } = default!;
        public string Role { get; set; } = "Student";             // Student | Admin
        public string PasswordHash { get; set; } = default!;      // store hash only
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PasswordUpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
