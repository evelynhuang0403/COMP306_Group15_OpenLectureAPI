namespace COMP306_Group15_OpenLectureAPI.DTOs
{
    public class UserDtos
    {
        public record UserReadDto(
            string UserId,
            string Email,
            string FullName,
            string Role,
            DateTime CreatedAt
            );

        public record UserCreateDto(
            string UserId,
            string Email,
            string FullName,
            string Role
            );

        public record UserUpdateDto(
            string Email,
            string FullName,
            string Role
            );

        public record UserPatchDto(
            string? Email,
            string? FullName,
            string? Role,
            bool? IsDeleted
            );
    }
}
