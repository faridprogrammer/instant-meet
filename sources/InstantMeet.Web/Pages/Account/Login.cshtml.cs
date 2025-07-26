using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using InstantMeet.Models;
using InstantMeet.Framework.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;

namespace InstantMeet.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IUserService _userService;

        public LoginModel(IUserService userService)
        {
            _userService = userService;
        }

        [BindProperty]
        public LoginViewModel Input { get; set; } = new();

        public string ReturnUrl { get; set; }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            if (!ModelState.IsValid)
                return Page();

            if (!await _userService.ValidateCredentials(Input.UserName, Input.Password))
            {
                ModelState.AddModelError("", "Invalid login");
                return Page();
            }

            var claims = new[] { new Claim(ClaimTypes.Name, Input.UserName) };
            var id = new ClaimsIdentity(claims,
                     CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                                         new ClaimsPrincipal(id));

            return LocalRedirect(returnUrl ?? "/");
        }
    }
}
