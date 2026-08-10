using Microsoft.AspNetCore.Mvc;
using StudentApi.DataSimulation;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using StudentApi.Model;
using Microsoft.AspNetCore.Authorization;

namespace StudentApi.Controllers
{
  
    [ApiController]
    [Route("/api/auth")]
    public class AuthController : Controller
    {
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
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: Cred
              );

            return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(Token)});
        }
    }
}
