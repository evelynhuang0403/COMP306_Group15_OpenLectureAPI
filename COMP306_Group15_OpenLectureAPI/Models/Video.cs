using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;

namespace COMP306_Group15_OpenLectureAPI.Models
{
    [DynamoDBTable("OpenLecture_Videos")]
    public class VideoItem
    {
        [DynamoDBHashKey] public string VideoId { get; set; } = default!;
        public string UploaderId { get; set; } = default!;

        public string Title { get; set; } = default!;
        public string Description { get; set; } = "";

        // Course context (replaces Category)
        public string Subject { get; set; } = "General";     // e.g., "Computer Science"
        public string CourseCode { get; set; } = "";         // e.g., "COMP306"
        public List<string> Tags { get; set; } = new();      // e.g., ["webapi","jwt","dynamodb"]

        // Lifecycle
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Visibility: "Public" | "Private" 
        public string Visibility { get; set; } = "Public";

        // Soft delete
        public bool IsDeleted { get; set; } = false;

        // S3 storage (minimal but practical)
        public string S3Bucket { get; set; } = default!;
        public string S3Key { get; set; } = default!;        // path/key to the video object
        public string ContentType { get; set; } = "video/mp4";
        public long? SizeBytes { get; set; }                 // optional; useful for UI/progress/limits

        // Denormalized counters
        public int ViewCount { get; set; }
        public int LikeCount { get; set; }
        public int DislikeCount { get; set; }
        public int CommentCount { get; set; }
    }
}


