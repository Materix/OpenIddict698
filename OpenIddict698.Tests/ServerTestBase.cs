using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Newtonsoft.Json;
using OpenIddict.Demo.Model;

namespace OpenIddict.Demo.Tests
{
    public class ServerTestBase
    {
        private const string ApplicationRole = "ApplicationRole";
        private const string TokenUri = "/connect/token";

        protected const string UserName = "username";
        protected const string Password = "password";

        private readonly DbContextOptions<ApplicationDbContext> _contextOptions;

        public TestServer TestServer { get; set; }

        public HttpClient Client { get; set; }

        public ServerTestBase()
        {
            var builder = new WebHostBuilder().UseContentRoot("..\\..\\..\\..\\OpenIddict698\\")
                                              .ConfigureAppConfiguration(
                                                  (hostingContext, config) =>
                                                  {
                                                      config.AddInMemoryCollection(new Dictionary<string, string>
                                                      {
                                                          { "InMemoryDatabaseName", Guid.NewGuid().ToString("N")}
                                                      });
                                                  })
                                              .ConfigureServices(services =>
                                              {
                                                  var startupAssembly = typeof(Startup).GetTypeInfo().Assembly;

                                                  // Inject a custom application part manager. Overrides AddMvcCore() because that uses TryAdd().
                                                  var manager = new ApplicationPartManager();
                                                  manager.ApplicationParts.Add(new AssemblyPart(startupAssembly));

                                                  manager.FeatureProviders.Add(new ControllerFeatureProvider());
                                                  manager.FeatureProviders.Add(new ViewComponentFeatureProvider());

                                                  services.AddSingleton(manager);

                                                  services.PostConfigure(
                                                      JwtBearerDefaults.AuthenticationScheme, (Action<JwtBearerOptions>)(options =>
                                                      {
                                                          options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>
                                                          (
                                                              Startup.ConfigurationPath,
                                                              new OpenIdConnectConfigurationRetriever(),
                                                              new HttpDocumentRetriever(Client)
                                                              {
                                                                  RequireHttps = false
                                                              }
                                                          );
                                                      }));
                                              })
                                              .UseEnvironment("test")
                                              .UseStartup<Startup>();

            TestServer = new TestServer(builder);
            var serviceProvider = TestServer.Host.Services;
            _contextOptions = serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();

            using (var context = CreateContext())
            {
                var userRole = new IdentityRole
                {
                    Name = ApplicationRole,
                    NormalizedName = ApplicationRole.ToUpper()
                };

                context.Add(userRole);
                context.SaveChanges();

                var user = new ApplicationUser
                {
                    NormalizedUserName = UserName.ToUpper(),
                    UserName = UserName,
                    SecurityStamp = Guid.NewGuid().ToString()
                };
                user.PasswordHash = serviceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>().HashPassword(user, Password);

                context.Add(user);
                context.Add(new IdentityUserRole<string>
                {
                    RoleId = userRole.Id,
                    UserId = user.Id
                });
                context.SaveChanges();
            }

            Client = TestServer.CreateClient();
        }

        protected ApplicationDbContext CreateContext()
        {
            return new ApplicationDbContext(_contextOptions);
        }

        protected async Task<(string, string)> AccessToken(string username, string password)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, TokenUri);
            var content = new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "username", username },
                { "password", password },
                { "resource", "http://localhost/" },
                { "scope", "offline_access roles" }
            };

            var formUrlEncodedContent = new FormUrlEncodedContent(content);
            formUrlEncodedContent.Headers.ContentType.MediaType = "application/x-www-form-urlencoded";
            request.Content = formUrlEncodedContent;

            var response = await Client.SendAsync(request);
            var result = await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync();

            var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);
            return (values["access_token"], values["refresh_token"]);
        }

        protected async Task<Dictionary<string, string>> RefreshToken(string accessToken, string refreshToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, TokenUri);
            request.Headers.Add("Authorization", "Bearer " + accessToken);
            var content = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
            };

            var formUrlEncodedContent = new FormUrlEncodedContent(content);
            formUrlEncodedContent.Headers.ContentType.MediaType = "application/x-www-form-urlencoded";
            request.Content = formUrlEncodedContent;

            var response = await Client.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Response status code does not indicate success: {response.StatusCode}, Content: {result}");
            }

            return JsonConvert.DeserializeObject<Dictionary<string, string>>(result);
        }

    }
}