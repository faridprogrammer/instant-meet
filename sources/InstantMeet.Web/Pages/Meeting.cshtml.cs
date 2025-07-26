using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InstantMeet.Web.Pages
{
    public class MeetingModel : PageModel
    {
        public string Room { get; set; }
        public string UserName { get; set; }

        public void OnGet()
        {
        }
    }
}
