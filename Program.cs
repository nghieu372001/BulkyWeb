using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository;
using Bulky.DataAccess.Repository.IRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Bulky.Utility;
using Microsoft.AspNetCore.Identity.UI.Services;
using Stripe;
using Bulky.DataAccess.DBInitializer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
// Add services: Entity Framework Core and using SQL Server
//In Net Core, when you add something to the service container, that way you are adding that to DI (Dependency Injection)
builder.Services.AddDbContext<ApplicationDbContext>(options=> 
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))); //builder.Configuration.GetConnectionString("DefaultConnection"): get string connect SQL Server from appsettings.json

//Config Stripe
//<StripeSetting>: class StripeSetting trong Bulky.Utility
//builder.Configuration.GetSection("Stripe") --> Go to part (phan`) "Stripe" in appsetting.json
//When implement class StripeSetting then app will retrieve value "SecretKey" and "Publishable" of "Stripe" on appsetting.json, and save it to proprty SecretKey, Publishable of class StripeSetting
builder.Services.Configure<StripeSetting>(builder.Configuration.GetSection("Stripe"));

//AddDefaultIdentity<IdentityUser> --> IdentityUser l� tham so kieu? (type parameter) xac dinh loai nguoi dung (user) trong he thong Identity, NOT HAVE ROLE
//builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true).AddEntityFrameworkStores<ApplicationDbContext>(); // NOT HAVE ROLE
//---------------------------------
//AddEntityFrameworkStores<ApplicationDbContext>(); --> when we add identity to the project, it basically also adds all the database table that are need for the identity. Like table user, role, ...All table identity will be manager by ApplicationDbContext 
//AddDefaultTokenProviders --> Using Token
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders(); // HAVE ROLE

//Config c�c options n�y sau builder.Services.AddIdentity
//Khai bao' cac' duong` dan~ cho phan` IDENTITY
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = $"/Identity/Account/Login";
    options.LogoutPath = $"/Identity/Account/Logout";
    options.AccessDeniedPath = $"/Identity/Account/AccessDenied";
});

//Add Service Login By Facebook
builder.Services.AddAuthentication().AddFacebook(options => {
    options.AppId = "308302908851872";
    options.AppSecret = "526ed3ccc54e29ffdc89642a55b14720";
});

//Add Session 
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>{
    options.IdleTimeout = TimeSpan.FromMinutes(100);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; 
});

//Add DI (Dependency Injection): When implement IDBInitializer interface then create new object from class DBInitializer
//AddScoped: run once time per request --> Using for DBInitializer (Khoi tao DB)
builder.Services.AddScoped<IDBInitializer, DBInitializer>();

//Add services support RazorPage, using for Identity
builder.Services.AddRazorPages();

//Add DI (Dependency Injection): When implement IUnitOfWork interface then create new object from class UnitOfWork
//AddScoped: run once time per request
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
//Add DI (Dependency Injection): When implement IEmailSender interface then create new object from class EmailSender
builder.Services.AddScoped<IEmailSender, EmailSender>();

var app = builder.Build();  

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//Use middleware
app.UseHttpsRedirection();
app.UseStaticFiles();

//Config Stripe
//builder.Configuration.GetSection("Stripe:SecretKey").Get<string>() --> Get value "SecretKey" of section "Stripe" in appsetting.json
StripeConfiguration.ApiKey = builder.Configuration.GetSection("Stripe:SecretKey").Get<string>();// ApiKey= "SecretKey"  trong appsetting.json


app.UseRouting();
app.UseAuthentication(); //Checking if username or password is valid ?. If it valid then go to UseAuthorization 
app.UseAuthorization(); //Access to website base on role. 
app.UseSession(); //Using Session
InitDatabase(); //Initial Data When Website Running First Time
app.MapRazorPages(); //Routing will map to the folder razor page --> URL se~ map voi' thu muc render cho view page: VD ../Identity/Account/Register --> using for identity (xac' thuc nguoi dung)
app.MapControllerRoute(
    name: "default",
    pattern: "{area=Customer}/{controller=Home}/{action=Index}/{id?}");

app.Run();




void InitDatabase()
{
    using(var scope = app.Services.CreateScope())
    {
        //implement of IDBInitializer
        //when meet interface IDBInitializer --> implement class DBInitializer in Bulky.DataAcess/DBInitializer/DBInitializer.cs
        var dbInitializer = scope.ServiceProvider.GetRequiredService<IDBInitializer>();
        dbInitializer.Initialize(); // run method Initialize in class DBInitializer in Bulky.DataAcess/DBInitializer/DBInitializer.cs
    }
}