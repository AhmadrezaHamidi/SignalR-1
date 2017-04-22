// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Sockets;

namespace ChatSample.Hubs
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly SignalRContext _signalR;

        public ChatController(SignalRContext signalR)
        {
            _signalR = signalR;
        }

        public async Task<IActionResult> Send(string message)
        {
            message = $"{HttpContext.User.Identity.Name}: {message}";

            var encoded = Encoding.UTF8.GetBytes(message);

            // TODO: InvokeAsync, but it's easy now :)
            await _signalR.All.SendAsync(new Message(encoded, MessageType.Text), CancellationToken.None);

            return StatusCode(StatusCodes.Status202Accepted);
        }
    }
}
