namespace COMP306_Group15_OpenLectureAPI.Models
{
    using Amazon.DynamoDBv2.DataModel;
    using System.Collections.Generic;

    [DynamoDBTable("OpenLecture_Playlists")]
    public class PlaylistItem
    {
        [DynamoDBHashKey] public string PlaylistId { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string OwnerId { get; set; } = default!;
        public string Visibility { get; set; } = "Public"; // Public | Private
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;

        // Store as a DynamoDB String Set (SS). Note: empty sets are NOT allowed by DynamoDB.
        public HashSet<string>? VideoIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
