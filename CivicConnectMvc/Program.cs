using CivicConnectMvc.Data;
using CivicConnectMvc.Infrastructure.Notifications;
using CivicConnectMvc.Infrastructure.Repositories;
using CivicConnectMvc.Interfaces;
using CivicConnectMvc.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("CivicConnectDb");
//why this is safer for SECRETS SETUP....
//b/c the app gets the connection string from appsettings.json, .NET User Secrets or another configured provider
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "The CivicConnectDb connection string is not configured.");

}

builder.Services.AddDbContext<CivicConnectDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
builder.Services.AddScoped<RequestService>();
builder.Services.AddScoped<IRequestService>(sp => sp.GetRequiredService<RequestService>());
builder.Services.AddScoped<IRequestSubject>(sp => sp.GetRequiredService<RequestService>());

// Observer + Adapter registrations.
builder.Services.AddScoped<IExternalNotificationClient, DemoNotificationClient>();
builder.Services.AddScoped<INotificationAdapter, NotificationAdapter>();
builder.Services.AddScoped<IRequestObserver, RequestNotificationObserver>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CivicConnectDbContext>();
    DbInitializer.Initialize(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
