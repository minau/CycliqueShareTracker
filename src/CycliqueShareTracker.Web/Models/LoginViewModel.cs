using System.ComponentModel.DataAnnotations;

namespace CycliqueShareTracker.Web.Models;

public sealed class LoginViewModel
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
