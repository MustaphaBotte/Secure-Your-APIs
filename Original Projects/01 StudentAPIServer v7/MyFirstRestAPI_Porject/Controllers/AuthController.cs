using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using StudentApi.DataSimulation;
using StudentApi.DTOs.Auth;
using StudentApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;

namespace StudentApi.Controllers
{

  
    [ApiController]
    [Route("/api/auth")]
    public class AuthController : Controller
    {
        private readonly ILogger<AuthController> _logger;

        public AuthController(ILogger<AuthController> logger)
        {
            _logger = logger;            
        }

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
                expires: DateTime.UtcNow.AddMinutes(10),
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
        [EnableRateLimiting("AuthLimiter")]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]

        public IActionResult Login([FromBody]LoginRequest loginRequest)
        {
            var student  = StudentDataSimulation.StudentsList.Find((student)=>student.Email == loginRequest.Email);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (student == null)
            {
                _logger.LogWarning(
                "Failed login attempt (email not found). Email={Email}, IP={IP}",
                loginRequest.Email,
                ip
                );
                return Unauthorized("Invalid credentials");
            }


            bool IsValidPassword = BCrypt.Net.BCrypt.Verify(loginRequest.Password, student?.PasswordHash);

            if (!IsValidPassword)
            {
                _logger.LogWarning(
               "Failed login attempt (bad password). Email={Email}, IP={IP}",
                loginRequest.Email,
                ip
                );
                return Unauthorized("Invalid credentials");
            }

            TokenResponse tokenResponse = GenerateTokenResponse(student);
            _logger.LogInformation(
                    "Successful login. UserId={UserId}, Email={Email}, IP={IP}",
                    student.Id,
                    student.Email,
                    ip
                   );
            return Ok(tokenResponse);
        }

        [HttpPost("refresh")]
        [EnableRateLimiting("AuthLimiter")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public IActionResult Refresh([FromBody] RefreshRequest refreshRequest)
        {

            var student = StudentDataSimulation.StudentsList.Find(stdnt => stdnt.Email == refreshRequest.Email);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // reusable local function
            IActionResult UnauthorizedWithLog(string message)
            {
                _logger.LogWarning("{Message} UserId={UserId}, Email={Email}, IP={IP}",
                    message,
                    student?.Id,
                    refreshRequest?.Email,
                    ip
                    );
                return Unauthorized(message);
            }

            if (student == null)
                return UnauthorizedWithLog("invalid refresh request");

            if(student.RefreshTokenRevokedAt != null)
                return UnauthorizedWithLog("refresh token is revoked");

            if(student.RefreshTokenExpiresAt == null || student.RefreshTokenExpiresAt <= DateTime.UtcNow)
                return UnauthorizedWithLog("refresh token is expired");

            if (!BCrypt.Net.BCrypt.Verify(refreshRequest.RefreshToken, student.RefreshTokenHash))
                return UnauthorizedWithLog("Invalid refresh token");


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
