using DragonSiege.Models;
using Microsoft.AspNetCore.Mvc;
namespace Controllers;
[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly UserService _userService;

    public UserController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public ActionResult<List<User>> GetAll()
    {
        var users = _userService.GetAll();
        return Ok(users);
    }

    [HttpGet("{id}")]
    public ActionResult<User> GetById(int id)
    {
        var user = _userService.GetById(id);
        if (user == null)
        {
            return NotFound();
        }
        return Ok(user);
    }
    [HttpPost]
    public ActionResult Create(User user)
    {
        try
        {
            if (!_userService.Add(user))
            {
                return BadRequest("Failed to create user.");
            }
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log the error)
            Console.WriteLine($"An error occurred: {ex.Message}");
            return StatusCode(500, "An error occurred while creating the user.");
        }
    }
    [HttpPut("{id}")]
    public ActionResult Update(int id, User user)
    {
        if (id != user.Id)
        {
            return BadRequest("ID mismatch.");
        }

        try
        {
            if (!_userService.Update(user))
            {
                return NotFound("User not found.");
            }
            return Ok(user);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return StatusCode(500, "An error occurred while updating the user.");
        }
    }
    [HttpDelete("{id}")]
    public ActionResult Delete(int id)
    {
        try
        {
            if (!_userService.Delete(id))
            {
                return NotFound("User not found.");
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return StatusCode(500, "An error occurred while deleting the user.");
        }
    }
}