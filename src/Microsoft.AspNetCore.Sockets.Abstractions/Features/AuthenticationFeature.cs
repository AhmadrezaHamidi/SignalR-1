using System.Security.Claims;

namespace Microsoft.AspNetCore.Sockets.Features
{
    public class AuthenticationFeature : IAuthenticationFeature
    {
        public ClaimsPrincipal User { get; set; }
    }
}
