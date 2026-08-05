using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryResponseDto>>> GetAll()
    {
        var categories = await context.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new CategoryResponseDto
            {
                Id = category.Id,
                Name = category.Name
            })
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryResponseDto>> GetById(int id)
    {
        var category = await context.Categories
            .AsNoTracking()
            .Where(category => category.Id == id)
            .Select(category => new CategoryResponseDto
            {
                Id = category.Id,
                Name = category.Name
            })
            .FirstOrDefaultAsync();

        if (category is null)
        {
            return NotFound();
        }

        return Ok(category);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponseDto>> Create(
        CategoryCreateDto dto)
    {
        var name = dto.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("Category name is required.");
        }

        var alreadyExists = await context.Categories
            .AnyAsync(category => category.Name == name);

        if (alreadyExists)
        {
            return Conflict("A category with this name already exists.");
        }

        var category = new Category
        {
            Name = name
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var response = new CategoryResponseDto
        {
            Id = category.Id,
            Name = category.Name
        };

        return CreatedAtAction(
            nameof(GetById),
            new { id = category.Id },
            response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        CategoryUpdateDto dto)
    {
        var category = await context.Categories.FindAsync(id);

        if (category is null)
        {
            return NotFound();
        }

        var name = dto.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("Category name is required.");
        }

        var alreadyExists = await context.Categories
            .AnyAsync(existingCategory =>
                existingCategory.Id != id &&
                existingCategory.Name == name);

        if (alreadyExists)
        {
            return Conflict("A category with this name already exists.");
        }

        category.Name = name;

        await context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await context.Categories
            .Include(category => category.Transactions)
            .FirstOrDefaultAsync(category => category.Id == id);

        if (category is null)
        {
            return NotFound();
        }

        if (category.Transactions.Count > 0)
        {
            return Conflict(
                "The category cannot be deleted because it contains transactions.");
        }

        context.Categories.Remove(category);
        await context.SaveChangesAsync();

        return NoContent();
    }
}