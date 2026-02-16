using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 7130;
});
builder.Services.AddIdentity<TodoListApp.Models.ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
    .AddEntityFrameworkStores<TodoListApp.Data.ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IPasswordHasher<TodoListApp.Models.ApplicationUser>, TodoListApp.Helpers.LegacyPasswordHasher>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;
});
builder.Services.AddHttpClient<TodoListApp.Services.IExternalApiService, TodoListApp.Services.ExternalApiService>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30); // Prevent hanging on slow APIs
    });
builder.Services.AddScoped<TodoListApp.Services.ITodoService, TodoListApp.Services.JsonFileTodoService>();
builder.Services.AddScoped<TodoListApp.Services.IEmailService, TodoListApp.Services.SmtpEmailService>();
builder.Services.AddScoped<TodoListApp.Services.ITimeTrackerService, TodoListApp.Services.JsonTimeTrackerService>();
builder.Services.AddHttpClient<TodoListApp.Services.ITranslationService, TodoListApp.Services.TranslationService>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });
builder.Services.AddScoped<TodoListApp.Services.IUserManagementService, TodoListApp.Services.UserManagementService>();
builder.Services.AddScoped<TodoListApp.Services.IFeatureService, TodoListApp.Services.FeatureService>();
builder.Services.AddScoped<TodoListApp.Services.IGoalTrackerService, TodoListApp.Services.GoalTrackerService>();

builder.Services.AddDbContext<TodoListApp.Data.ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=todotask.db"));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TodoListApp.Services.IPdfService, TodoListApp.Services.PdfService>();
builder.Services.AddHostedService<TodoListApp.Services.PdfCleanupService>();

builder.Services.AddMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Seed Roles
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await TodoListApp.Data.DbInitializer.SeedRolesAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding roles.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();


app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Todo}/{action=Dashboard}/{id?}")
    .WithStaticAssets();


app.Run();
