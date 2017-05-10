using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Sockets.Features;

namespace Microsoft.AspNetCore.Sockets.Features
{
    public class AuthenticationFeature : IAuthenticationFeature
    {
        public ClaimsPrincipal User { get; set; }
    }
}