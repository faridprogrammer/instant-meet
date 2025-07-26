using System.ComponentModel.DataAnnotations;

namespace InstantMeet.Models;

public class LoginViewModel
{
  [Required]
  public string UserName { get; set; } = default!;

  [Required, DataType(DataType.Password)]
  public string Password { get; set; } = default!;
}
