using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Dtos;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
public class AuthController : ControllerBase
{
    private readonly UserManager<TmsUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly TmsDbContext _dbContext;

    public AuthController(
        UserManager<TmsUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ITokenService tokenService,
        TmsDbContext dbContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _dbContext = dbContext;
    }

    [EnableRateLimiting("LoginPolicy")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { detail = "Email and password are required." });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Unauthorized(new { detail = "Invalid email or password." });
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return StatusCode(StatusCodes.Status423Locked, new { detail = "User account is locked out. Please try again later." });
        }

        var isValidPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValidPassword)
        {
            await _userManager.AccessFailedAsync(user);
            if (await _userManager.IsLockedOutAsync(user))
            {
                return StatusCode(StatusCodes.Status423Locked, new { detail = "User account is locked out due to multiple failed login attempts." });
            }
            return Unauthorized(new { detail = "Invalid email or password." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, roles);
        var refreshTokenString = _tokenService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Token = refreshTokenString,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync();

        return Ok(new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshTokenString,
            UserId: user.Id,
            Email: user.Email ?? string.Empty,
            FirstName: user.FirstName,
            LastName: user.LastName
        ));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new { detail = "RefreshToken is required." });
        }

        var existingToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken);

        if (existingToken == null)
        {
            return Unauthorized(new { detail = "Invalid refresh token." });
        }

        if (existingToken.IsRevoked)
        {
            return Unauthorized(new { detail = "Refresh token has been revoked." });
        }

        if (existingToken.IsExpired)
        {
            return Unauthorized(new { detail = "Refresh token has expired." });
        }

        var user = await _userManager.FindByIdAsync(existingToken.UserId);
        if (user == null)
        {
            return Unauthorized(new { detail = "User not found." });
        }

        // Perform token rotation
        var newRefreshTokenString = _tokenService.GenerateRefreshToken();

        existingToken.IsRevoked = true;
        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.ReplacedByToken = newRefreshTokenString;

        var newRefreshToken = new RefreshToken
        {
            Token = newRefreshTokenString,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        _dbContext.RefreshTokens.Add(newRefreshToken);
        await _dbContext.SaveChangesAsync();

        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = _tokenService.GenerateAccessToken(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, roles);

        return Ok(new RefreshResponse(
            AccessToken: newAccessToken,
            RefreshToken: newRefreshTokenString
        ));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { detail = "Email and password are required." });
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            return BadRequest(new { detail = "User with this email already exists." });
        }

        var user = new TmsUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                detail = "Password does not meet complexity requirements or user creation failed.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        var role = string.IsNullOrWhiteSpace(request.Role) ? "Student" : request.Role;
        if (!await _roleManager.RoleExistsAsync(role))
        {
            await _roleManager.CreateAsync(new IdentityRole(role));
        }
        await _userManager.AddToRoleAsync(user, role);

        return Ok(new
        {
            userId = user.Id,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            role
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { detail = "User identifier not found in claims." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { detail = "User not found." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new
        {
            userId = user.Id,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            roles
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(new { message = "Logged out successfully" });
    }
}