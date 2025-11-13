namespace COMP306_Group15_OpenLectureAPI.DTOs
{
    public class AuthDtos
    {
        public record RegisterRequestDto(
            string Email, 
            string Password, 
            string FullName
            );

        public record RegisterResponseDto(
            string UserId, 
            string Email, 
            string FullName, 
            string Role
            );

        public record LoginRequestDto(
            string Email, 
            string Password
            );

        public record LoginResponseDto(
            string Token, 
            string UserId, 
            string FullName, 
            string Role
            );
    }
}
