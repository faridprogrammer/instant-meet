using InstantMeet.Web;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace InstantMeet.Pages;

public class IndexModel : PageModel
{
    public string CurrentUserName { get; private set; }
    public string StunServer { get; private set; }
    private readonly ILogger<IndexModel> _logger;
    private readonly IOptions<WebRTCSettings> webRtcSettings;

    public IndexModel(ILogger<IndexModel> logger, IOptions<WebRTCSettings> webRtcSettings)
    {
        _logger = logger;
        this.webRtcSettings = webRtcSettings;
    }

    public void OnGet()
    {
        CurrentUserName = User.Identity?.Name;
        StunServer = webRtcSettings.Value.StunServer;
        
        if (string.IsNullOrEmpty(CurrentUserName))
        {
            RedirectToPage("Account/Login");
        }

    }
}
