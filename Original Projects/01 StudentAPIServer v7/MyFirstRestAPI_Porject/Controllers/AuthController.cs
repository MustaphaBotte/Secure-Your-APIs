using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using StudentApi.DataSimulation;
using StudentApi.DTOs.Auth;
using StudentApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace StudentApi.Controllers
{

  
    [ApiController]
    [Route("/api/auth")]
    public class AuthController : Controller
    {
        private TokenResponse GenerateTokenResponse(Student student)
        {
            Claim[] payload = new Claim[] {
              new Claim(ClaimTypes.NameIdentifier.ToString(),student?.Id.ToString()??"N/A"),
              new Claim(ClaimTypes.Email.ToString(),student.Email),
              new Claim(ClaimTypes.Role,student.Role)
            };


            var Key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("THIS_IS_A_VERY_SECRET_KEY_123456"));

            var Cred = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256);

            var Token = new JwtSecurityToken(
                issuer: "AtlasSchool",
                audience: "students",
                claims: payload,
                expires: DateTime.UtcNow.AddSeconds(10),
                signingCredentials: Cred
              );
            var accessToken = new JwtSecurityTokenHandler().WriteToken(Token);
            var refreshToken = GenerateRefreshToken();

            student.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);
            student.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7);
            student.RefreshTokenRevokedAt = null;

            return new TokenResponse { RefreshToken=refreshToken, AccessToken=accessToken };

        }



        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Login([FromBody]LoginRequest loginRequest)
        {
            var student  = StudentDataSimulation.StudentsList.Find((student)=>student.Email == loginRequest.Email);

            if(student == null) 
                 return Unauthorized("Wrong email or password");
            
            bool IsValidPassword = BCrypt.Net.BCrypt.Verify(loginRequest.Password, student?.PasswordHash);
          
            if (!IsValidPassword)
                 return Unauthorized("Wrong email or password");


            TokenResponse tokenResponse = GenerateTokenResponse(student);
            return Ok(tokenResponse);
        }

        [HttpPost("refresh")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Refresh([FromBody] RefreshRequest refreshRequest)
        {

            var student = StudentDataSimulation.StudentsList.Find(stdnt => stdnt.Email == refreshRequest.Email);

            if (student == null)
                return Unauthorized("invalid refresh request");

            if(student.RefreshTokenRevokedAt != null)
                return Unauthorized("refresh token is revoked");

            if(student.RefreshTokenExpiresAt == null || student.RefreshTokenExpiresAt <= DateTime.UtcNow)
                return Unauthorized("refresh token is expired");


            if (!BCrypt.Net.BCrypt.Verify(refreshRequest.RefreshToken, student.RefreshTokenHash))
                return Unauthorized("Invalid refresh token");


            TokenResponse tokenResponse = GenerateTokenResponse(student);

            return Ok(tokenResponse);
        }

        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Logout([FromBody] LogoutRequest logoutRequest)
        {
            var student = StudentDataSimulation.StudentsList.Find(stdnt => stdnt.Email == logoutRequest.Email);

            if (student == null)
                return Ok();

            if (!BCrypt.Net.BCrypt.Verify(logoutRequest.RefreshToken, student.RefreshTokenHash))
                return Ok();

            student.RefreshTokenRevokedAt = DateTime.UtcNow;
            return Ok("Logged out successfully");
        }

        private static string GenerateRefreshToken()
        {
            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }



    }
}
