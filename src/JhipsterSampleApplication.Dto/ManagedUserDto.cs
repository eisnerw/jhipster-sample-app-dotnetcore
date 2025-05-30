using System.ComponentModel.DataAnnotations;

namespace JhipsterSampleApplication.Dto;

public class ManagedUserDto : UserDto
{
    public const int PasswordMinLength = 4;

    public const int PasswordMaxLength = 100;

    [Required]
    [MinLength(PasswordMinLength)]
    [MaxLength(PasswordMaxLength)]
    public string Password { get; set; } = string.Empty;

    public override string ToString()
    {
        return "ManagedUserDto{} " + base.ToString();
    }
}
