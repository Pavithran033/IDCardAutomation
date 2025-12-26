using IDCardAutomation.Services;
using IDCardAutomation.Utils;
using DinkToPdf;
using DinkToPdf.Contracts;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddRazorPages();

//to download pdf

// ?? Add session services
builder.Services.AddDistributedMemoryCache(); // Required for storing session data
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<IDCardAutomation.Utils.EmailSender>();

builder.Services.AddHttpContextAccessor();




var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ?? Enable session
app.UseSession(); // ?? Important: must be placed before UseAuthorization()

app.UseAuthorization();

app.MapRazorPages();

app.Run();

