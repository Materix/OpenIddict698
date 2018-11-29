using System;
using AspNet.Security.OpenIdConnect.Primitives;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Demo.Model;

namespace OpenIddict.Demo
{
    public class Startup
    {
        public const string ConfigurationPath = "/api/.well-known/openid-configuration";
        public const string CryptographyPath = "/api/.well-known/jwks";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc()
                    .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(Configuration["InMemoryDatabaseName"]);
                options.UseOpenIddict();
            });

            services.AddIdentity<ApplicationUser, IdentityRole>()
                    .AddEntityFrameworkStores<ApplicationDbContext>()
                    .AddDefaultTokenProviders();

            services.AddOpenIddict()
                    .AddCore(
                        options =>
                        {
                            options.UseEntityFrameworkCore()
                                   .UseDbContext<ApplicationDbContext>();
                        })
                    .AddServer(
                        options =>
                        {
                            options.UseMvc();

                            options.RegisterScopes(
                                       OpenIdConnectConstants.Scopes.OpenId,
                                       OpenIdConnectConstants.Scopes.Email,
                                       OpenIdConnectConstants.Scopes.Profile,
                                       OpenIdConnectConstants.Scopes.OfflineAccess,
                                       OpenIddictConstants.Scopes.Roles)
                                   .AcceptAnonymousClients();

                            options.UseJsonWebTokens()
                                   .UseRollingTokens()
                                   .EnableTokenEndpoint("/connect/token")
                                   .AllowPasswordFlow()
                                   .AllowRefreshTokenFlow()
                                   .DisableHttpsRequirement()
                                   .AddEphemeralSigningKey()
                                   .SetAccessTokenLifetime(TimeSpan.FromMinutes(15))
                                   .SetRefreshTokenLifetime(TimeSpan.FromMinutes(15))
                                   .Configure(
                                       o =>
                                       {
                                           o.ConfigurationEndpointPath = ConfigurationPath;
                                           o.CryptographyEndpointPath = CryptographyPath;
                                       });
                        });

            services.AddAuthentication(
                        options =>
                        {
                            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                        })
                    .AddJwtBearer(
                        options =>
                        {
                            options.TokenValidationParameters.NameClaimType = OpenIdConnectConstants.Claims.Name;
                            options.RequireHttpsMetadata = false;
                            options.Audience = "resource_server";
                            options.Authority = "http://localhost/";
                            options.MetadataAddress = "http://localhost/" + ConfigurationPath;
                        });
            services.Configure<IdentityOptions>(
                options =>
                {
                    options.ClaimsIdentity.UserNameClaimType = OpenIdConnectConstants.Claims.Name;
                    options.ClaimsIdentity.UserIdClaimType = OpenIdConnectConstants.Claims.Subject;
                    options.ClaimsIdentity.RoleClaimType = OpenIdConnectConstants.Claims.Role;
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseAuthentication();
            app.UseMvc();
        }
    }
}