using System.Security.Claims;

namespace Microsoft.AspNetCore.Sockets.Features
{
    public interface IAuthenticationFeature
    {
        ClaimsPrincipal User { get; set; }
    }
}
