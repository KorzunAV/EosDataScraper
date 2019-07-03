using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Cryptography.ECDSA;
using EosDataScraper.Api.Contexts;
using EosDataScraper.Api.Entities;
using EosDataScraper.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace EosDataScraper.Api.Controllers
{
    [Produces("application/json")]
    [Route("api/v1/[controller]")]
    [ApiController]
    public class AuthController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly PostgresDbContext _postgresDbContext;

        public AuthController(IConfiguration configuration, PostgresDbContext postgresDbContext)
        {
            _configuration = configuration;
            _postgresDbContext = postgresDbContext;
        }

        /// <summary>
        /// SignUp / SignIn.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/v1/Auth
        ///     {
        ///         "login": "Alex@gmail.com",
        ///         "password": "admin1234"
        ///     }
        ///
        /// </remarks>
        /// <param name="request">User login and password</param>
        /// <returns>Access token</returns>
        /// <response code="200">Returns access token</response>
        /// <response code="409">If user already exist and password not match</response>  
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<TokenResponse>> Post(AuthRequest request)
        {
            var pHash = Sha256Manager.GetHash(Encoding.UTF8.GetBytes(request.Login + request.Password + "6998AD68-8F11-41B2-9627-CBC34C5E68C4"));
            var user = await _postgresDbContext.Users.FirstOrDefaultAsync(u => u.Login.Equals(request.Login));

            if (user != null)
            {
                if (!user.Password.SequenceEqual(pHash))
                    return new ConflictResult();
            }
            else
            {
                user = new UserEntity
                {
                    Login = request.Login,
                    Password = pHash,
                    Role = Roles.User
                };
                await _postgresDbContext.Users.AddAsync(user);
                await _postgresDbContext.SaveChangesAsync();
            }

            var jwtSettings = new JwtSettings(DateTime.UtcNow);
            _configuration.GetSection(nameof(JwtSettings))
                .Bind(jwtSettings);

            var claims = new[]
            {
                new Claim(nameof(UserEntity.Id), user.Id.ToString()),
                new Claim(ClaimsIdentity.DefaultNameClaimType, user.Login),
                new Claim(ClaimsIdentity.DefaultRoleClaimType, user.Role.ToString()),
                new Claim(nameof(TokenResponse.Expires), jwtSettings.Expires.ToString())
            };

            var jwt = new JwtSecurityToken(
                jwtSettings.Issuer,
                jwtSettings.Audience,
                notBefore: jwtSettings.Now.UtcDateTime,
                claims: claims,
                expires: jwtSettings.Expires.UtcDateTime,
                signingCredentials: new SigningCredentials(jwtSettings.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256)
            );

            var result = new TokenResponse
            {
                Token = new JwtSecurityTokenHandler().WriteToken(jwt),
                Expires = jwtSettings.Expires
            };

            return new JsonResult(result);
        }
    }
}