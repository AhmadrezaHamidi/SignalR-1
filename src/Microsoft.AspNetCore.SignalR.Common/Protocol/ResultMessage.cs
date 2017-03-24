// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.SignalR.Protocol
{
    public class ResultMessage : HubMessage
    {
        public object Payload { get; }
        public string Error { get; }

        public ResultMessage(long invocationId, string error, object payload) : base(invocationId)
        {
            Payload = payload;
            Error = error;
        }

        public override string ToString()
        {
            return $"Result {{ id:{InvocationId}, error: \"{Error}\", payload: {Payload} ] }}";
        }
    }
}
