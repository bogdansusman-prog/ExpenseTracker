using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FinancialTransactionResponseDto>>> GetAll()
    {
        var transactions = await context.Transactions
            .AsNoTracking()
            .OrderByDescending(transaction => transaction.Date)
            .Select(transaction => new FinancialTransactionResponseDto
            {
                Id = transaction.Id,
                Title = transaction.Title,
                Amount = transaction.Amount,
                Date = transaction.Date,
                Type = transaction.Type,
                Description = transaction.Description,
                CategoryId = transaction.CategoryId,
                CategoryName = transaction.Category.Name
            })
            .ToListAsync();

        return Ok(transactions);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FinancialTransactionResponseDto>> GetById(int id)
    {
        var transaction = await context.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.Id == id)
            .Select(transaction => new FinancialTransactionResponseDto
            {
                Id = transaction.Id,
                Title = transaction.Title,
                Amount = transaction.Amount,
                Date = transaction.Date,
                Type = transaction.Type,
                Description = transaction.Description,
                CategoryId = transaction.CategoryId,
                CategoryName = transaction.Category.Name
            })
            .FirstOrDefaultAsync();

        if (transaction is null)
        {
            return NotFound();
        }

        return Ok(transaction);
    }

    [HttpPost]
    public async Task<ActionResult<FinancialTransactionResponseDto>> Create(
        FinancialTransactionRequestDto dto)
    {
        var title = dto.Title.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest("Transaction title is required.");
        }

        if (dto.Amount <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        if (!Enum.IsDefined(typeof(TransactionType), dto.Type))
        {
            return BadRequest("Transaction type is invalid.");
        }

        var category = await context.Categories.FindAsync(dto.CategoryId);

        if (category is null)
        {
            return BadRequest("The selected category does not exist.");
        }

        var transaction = new FinancialTransaction
        {
            Title = title,
            Amount = dto.Amount,
            Date = dto.Date?.UtcDateTime ?? DateTime.UtcNow,
            Type = dto.Type,
            CategoryId = dto.CategoryId,
            Description = dto.Description?.Trim()
        };

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();

        var response = new FinancialTransactionResponseDto
        {
            Id = transaction.Id,
            Title = transaction.Title,
            Amount = transaction.Amount,
            Date = transaction.Date,
            Type = transaction.Type,
            Description = transaction.Description,
            CategoryId = transaction.CategoryId,
            CategoryName = category.Name
        };

        return CreatedAtAction(
            nameof(GetById),
            new { id = transaction.Id },
            response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        FinancialTransactionRequestDto dto)
    {
        var transaction = await context.Transactions.FindAsync(id);

        if (transaction is null)
        {
            return NotFound();
        }

        var title = dto.Title.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest("Transaction title is required.");
        }

        if (dto.Amount <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        if (!Enum.IsDefined(typeof(TransactionType), dto.Type))
        {
            return BadRequest("Transaction type is invalid.");
        }

        var categoryExists = await context.Categories
            .AnyAsync(category => category.Id == dto.CategoryId);

        if (!categoryExists)
        {
            return BadRequest("The selected category does not exist.");
        }

        transaction.Title = title;
        transaction.Amount = dto.Amount;
        transaction.Type = dto.Type;
        transaction.CategoryId = dto.CategoryId;
        transaction.Description = dto.Description?.Trim();

        if (dto.Date.HasValue)
        {
            transaction.Date = dto.Date.Value.UtcDateTime;
        }

        await context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var transaction = await context.Transactions.FindAsync(id);

        if (transaction is null)
        {
            return NotFound();
        }

        context.Transactions.Remove(transaction);
        await context.SaveChangesAsync();

        return NoContent();
    }
}