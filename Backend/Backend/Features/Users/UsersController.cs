using System.Net.Mime;
using Backend.Auth.Users;
using Backend.Features.Users.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Users;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "manager")]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
public class UsersController : ControllerBase
{
    private readonly RoleManager<Role> _roleManager;
    private readonly UserManager<User> _userManager;

    public UsersController(UserManager<User> userManager, RoleManager<Role> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserView>>> Get()
    {
        return Ok(await _userManager.Users.Select(
            user => new UserView
            {
                Id = user.Id,
                Email = user.Email,
                RoleId = GetRoleId(user).Result
            }).ToListAsync());
    }

    [HttpGet("{role}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<UserView>>> GetAllbyRole([FromRoute]string role)
    {
        var roleModel = _roleManager.FindByNameAsync(role);
        if (roleModel is null)
        {
            return NotFound("Role not found");
        }

        return Ok(await _userManager.GetUsersInRoleAsync(role));
    }
    
    [HttpGet("id/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserView>> GetbyId([FromRoute]string id)
    {
        return (await _userManager.FindByIdAsync(id)) is null ? Ok(await _userManager.FindByIdAsync(id)) : NotFound();
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserView>> Post([FromBody]UserRequest request)
    {
        var newUser = new User { Email = request.Email };

        var result = await _userManager.CreateAsync(newUser, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest();
        }

        var role = await _roleManager.FindByIdAsync(request.RoleId);
        if (role is null)
        {
            return NotFound("Role not found");
        }

        var user = await _userManager.FindByEmailAsync(newUser.Email);
        if (user is null)
        {
            return NotFound("User could not be created");
        }

        await _userManager.AddToRoleAsync(user, role.Name);

        return Created($"users/{user.Id}", new UserView
        {
            Id = user.Id,
            Email = user.Email,
            RoleId = role.Id
        });
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserView>> Delete([FromRoute] string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound("User not found");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return Ok(StatusCodes.Status409Conflict);
        }

        return Ok(new UserView
        {
            Id = user.Id,
            Email = user.Email,
            RoleId = await GetRoleId(user)
        });
    }

    [HttpPatch("role/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserView>> ChangeRole([FromRoute] string id, [FromBody]string roleId)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound("User not found");
        }

        var oldRole = await _roleManager.FindByNameAsync((await _userManager.GetRolesAsync(user)).First());

        var newRole = await _roleManager.FindByIdAsync(roleId);
        if (newRole is null)
        {
            return NotFound("Role not found");
        }

        await _userManager.RemoveFromRoleAsync(user, oldRole.Name);
        await _userManager.AddToRoleAsync(user, newRole.Name);
        var result = await _userManager.UpdateAsync(user);

        return Ok(new UserView
        {
            Id = user.Id,
            Email = user.Email,
            RoleId = await GetRoleId(user)
        });
    }

    private async Task<string> GetRoleId(User user)
    {
        var roleName = (await _userManager.GetRolesAsync(user)).First();
        return (await _roleManager.FindByNameAsync(roleName)) is null
            ? (await _roleManager.FindByNameAsync(roleName)).Id
            : null;
    }
}