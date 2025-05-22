using Messanger.Hubs;
using Messanger.Models;
using Messanger.Models.ViewModels;
using Messanger.Services;     
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Infrastructure;  
using Microsoft.AspNetCore.Mvc;
using Messanger.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<MessengerContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MessangerConnection")));

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddSignalR(options => options.EnableDetailedErrors = true);


builder.Services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();


builder.Services.AddScoped<IHandler<RegisterViewModel>, ModelStateHandler>();
builder.Services.AddScoped<IHandler<RegisterViewModel>, DuplicateEmailHandler>();
builder.Services.AddScoped<IHandler<RegisterViewModel>, PasswordMatchHandler>();
builder.Services.AddScoped<IHandler<RegisterViewModel>, AvatarSizeHandler>();
builder.Services.AddSingleton<IChatNotifier, SignalRChatNotifier>();
builder.Services.AddSingleton<IPasswordHasher, Sha256PasswordHasher>();
builder.Services.AddScoped<IFileService, FileService>();
var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chatHub");
app.MapHub<ProfileHub>("/profileHub");
app.MapHub<GroupHub>("/groupHub");

app.Run();
