using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using COMP306_Group15_OpenLectureAPI.Models;
using Microsoft.IdentityModel.Tokens;

namespace COMP306_Group15_OpenLectureAPI.Data
{
    public static class JwtHelper
    {
        public static string IssueToken(IConfiguration cfg, UserItem user)
        {
            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Email, user.Email)
            };

            var token = new JwtSecurityToken(
                issuer: cfg["Jwt:Issuer"],
                audience: cfg["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(6),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static string? UserId(ClaimsPrincipal p)
        {
            return p.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? p.FindFirstValue(ClaimTypes.NameIdentifier); // fallback
        }

        public static bool IsAdmin(ClaimsPrincipal p) => p.IsInRole("Admin");
    }
}
