using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos;

public class CategoryCreateDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class CategoryUpdateDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class CategoryResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}