using InstantMeet;
using InstantMeet.Framework.Data;
using InstantMeet.Framework.Services;
using InstantMeet.Web;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
);

builder.Services.Configure<WebRTCSettings>(builder.Configuration.GetSection("WebRTCSettings"));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"), b => b.MigrationsAssembly("InstantMeet.Framework")));

builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
       .AddCookie(options =>
       {
           options.ExpireTimeSpan = TimeSpan.FromDays(1);
           options.LoginPath = "/Account/Login";
           options.LogoutPath = "/Account/Login";
       });


builder.Services.AddSignalR();
builder.Services.AddRazorPages(options =>
{
});

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Production error handling or specific settings
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapHub<InstantMeetHub>("/instantMeetHub");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    Console.WriteLine($"Database file path: {dbContext.GetAbsoluteDatabasePath()}");
    dbContext.Database.Migrate();
}

app.Run();
