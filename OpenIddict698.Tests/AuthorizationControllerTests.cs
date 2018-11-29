using System;
using System.Threading.Tasks;
using Xunit;

namespace OpenIddict.Demo.Tests
{
    public class AuthorizationControllerTests : ServerTestBase
    {
        [Fact]
        public async Task TestA()
        {
            // Given
            var (accessToken, refreshToken) = await AccessToken(UserName, Password);

            // When
            var values = await RefreshToken(accessToken, refreshToken);

            // Then
            ValidateNotEmpty(values["access_token"]);
            ValidateNotEmpty(values["refresh_token"]);
        }

        [Fact]
        public async Task TestB()
        {
            // Given
            var (accessToken, refreshToken) = await AccessToken(UserName, Password);

            // When
            var values = await RefreshToken(accessToken, refreshToken);


            // Then
            ValidateNotEmpty(values["access_token"]);
            ValidateNotEmpty(values["refresh_token"]);
        }

        private void ValidateNotEmpty(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Value cannot be null or empty", nameof(value));
            }
        }
    }
}
