using HouseRentingSystemApi.Data.Entities;
using HouseRentingSystemApi.Models.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HouseRentingSystemApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces(typeof(AuthResult))]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration configuration;

        public AuthController(UserManager<ApplicationUser> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            this.configuration = configuration;
        }

        [HttpPost("/login")]
        public async Task<IActionResult> Login([FromBody]LoginModel model)
        {
            if (!ModelState.IsValid)
            {
                var authResult = new AuthResult();
                authResult.Code = 400;
                
            var allErrors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                authResult.Massage = string.Join(Environment.NewLine, allErrors);
                return Unauthorized();
            }
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                return Unauthorized(populateresult(400,null,"The username or email do not exists."));
            }
            var result = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!result)
            {
                return Unauthorized(populateresult(400, null, "The username or email already exists."));
            }
            var token = GenerateJwtToken(user);
            Console.WriteLine(token);
            return Ok("\"" + token + "\"");
            //return Ok(populateresult(200,token,"User logged in"));
        }

      

        [HttpPost("/register")]
        [Produces(typeof(AuthResult))]
        public async Task <IActionResult> Register([FromBody]Register model)
        {
           if(!ModelState.IsValid)
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user != null)
            {
                return Ok(populateresult(400, null, "User already exist"));
                
            }
            var newUser = new ApplicationUser()
            {
                Email = model.Email,
                UserName = model.Username
            };
           var result = await _userManager.CreateAsync( newUser,model.Password);
            
            if(result.Succeeded)
            {
              

                return Ok(populateresult(200,null, "User registered succsesfully"));
            }
           
            return BadRequest();  
        }

        private string GenerateJwtToken(ApplicationUser user)
        {
            var jwtSection = configuration.GetSection("Jwt");
            var key = jwtSection["Key"]!;

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName!),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName!)
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var expires = DateTime.UtcNow.AddMinutes(
              int.Parse(jwtSection["ExpiresInMinutes"]!)
            );

            var token = new JwtSecurityToken(
                issuer: jwtSection["Issuer"],
                audience: jwtSection["Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private AuthResult populateresult( int code, string? token = null,params string[] masseges)
        {
            var result = new AuthResult();
            result.Code = code;
            result.Massage = string.Join(Environment.NewLine, masseges);
            if(token != null)
            {
                result.Token = token;
            }
            return result;
        }


    }
}
