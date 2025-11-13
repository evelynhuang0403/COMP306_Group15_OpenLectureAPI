namespace COMP306_Group15_OpenLectureAPI.DTOs
{
    public class PlaylistDtos
    {
        public record PlaylistReadDto(
            string PlaylistId, 
            string Name, 
            string OwnerId, 
            string Visibility, 
            DateTime CreatedAt, 
            bool IsDeleted, 
            List<string> VideoIds
            );

        public record PlaylistCreateDto(
            string Name, 
            string OwnerId, 
            string Visibility,  // Public | Private
            IList<string>? VideoIds

            );

        public record PlaylistUpdateDto(
                string Name,
                string Visibility,
                IList<string> VideoIds
            );

        public record PlaylistPatchDto(
            string? Name, 
            string? Visibility
            );

        public record PlaylistVideoChangeDto(string VideoId);

    }
}
