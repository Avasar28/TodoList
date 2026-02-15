using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 7130;
});
builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options =>
    {
        options.LoginPath = "/Account/Login";
        options.Cookie.HttpOnly = true; // Prevent XSS
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Require HTTPS
        options.Cookie.SameSite = SameSiteMode.Strict; // Prevent CSRF
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
    });
builder.Services.AddHttpClient<TodoListApp.Services.IExternalApiService, TodoListApp.Services.ExternalApiService>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30); // Prevent hanging on slow APIs
    });
builder.Services.AddScoped<TodoListApp.Services.ITodoService, TodoListApp.Services.JsonFileTodoService>();
builder.Services.AddScoped<TodoListApp.Services.IUserService, TodoListApp.Services.JsonUserService>();
builder.Services.AddScoped<TodoListApp.Services.IEmailService, TodoListApp.Services.SmtpEmailService>();
builder.Services.AddScoped<TodoListApp.Services.ITimeTrackerService, TodoListApp.Services.JsonTimeTrackerService>();
builder.Services.AddHttpClient<TodoListApp.Services.ITranslationService, TodoListApp.Services.TranslationService>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });

builder.Services.AddDbContext<TodoListApp.Data.ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=todotask.db"));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TodoListApp.Services.IPdfService, TodoListApp.Services.PdfService>();

builder.Services.AddMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

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
