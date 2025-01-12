using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using System.Text;
using System.Security.Claims;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ServerContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ServerContextSQLite")));
builder.Services.AddDbContext<SecurityContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SecurityContextSQLite")));
    
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<User, Role>(cfg => {
    
}).AddEntityFrameworkStores<SecurityContext>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(o => o.LoginPath = new PathString("/auth/login"))
                .AddJwtBearer(opts => {
                    opts.RequireHttpsMetadata = false;
                    opts.SaveToken = true;
                    opts.TokenValidationParameters = new TokenValidationParameters {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.ASCII.GetBytes(builder.Configuration["jwtSecret"])
                        ),
                        ValidateAudience = false,
                        ValidateIssuer = false,
                    };
                    opts.Events = new JwtBearerEvents {
                        OnTokenValidated = async ctx => {
                            var usrmgr = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
                            var signinmgr = ctx.HttpContext.RequestServices.GetRequiredService<SignInManager<User>>();
                            string? username = ctx.Principal?.FindFirst(ClaimTypes.Name)?.Value;
                            User idUser = await usrmgr.FindByNameAsync(username);
                            ctx.Principal = await signinmgr.CreateUserPrincipalAsync(idUser);
                        },
                    };
                });

builder.Services.Configure<IdentityOptions>(opts => {
    opts.Password.RequireLowercase = false;
    opts.Password.RequiredLength = 6;
    opts.Password.RequireUppercase = false;
    opts.Password.RequireNonAlphanumeric = false;
    opts.Password.RequireDigit = false;
    opts.User.RequireUniqueEmail = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ServerContext>();
    context.Database.EnsureCreated();
    var securityContext = services.GetRequiredService<SecurityContext>();
    securityContext.Database.EnsureCreated();
    DbInitializer.Initialize(context);
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
