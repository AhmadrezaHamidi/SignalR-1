using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Sockets.Features;

namespace Microsoft.AspNetCore.Sockets
{
    public class DefaultConnectionContext : ConnectionContext
    {
        private static readonly Func<IAuthenticationFeature> _newAuthenticationFeature = () => new AuthenticationFeature();
        private static readonly Func<IItemsFeature> _newItemsFeature = () => new ItemsFeature();

        // REVIEW(anurse):
        //  These two are a little weird. They have exactly what we want but are named using Request-related names (RequestServices, RequestAborted, etc.),
        //  which is a little too HTTPy for this API which is designed to work outside HTTP
        //  We'd probably just create separate ones to make it clear which is connection-oriented and which is request-oriented
        private static readonly Func<IServiceProvidersFeature> _newServiceProvidersFeature = () => new ServiceProvidersFeature();
        private static readonly Func<IHttpRequestLifetimeFeature> _newRequestLifetimeFeature = () => new HttpRequestLifetimeFeature();

        private IFeatureCollection _features;

        public override string ConnectionId => Features.Get<IHttpConnectionFeature>().ConnectionId;
        public override IFeatureCollection Features => _features;

        public override ClaimsPrincipal User
        {
            get => GetOrCreateFeature(_newAuthenticationFeature).User;
            set => GetOrCreateFeature(_newAuthenticationFeature).User = value;
        }

        public override IDictionary<object, object> Items
        {
            get => GetOrCreateFeature(_newItemsFeature).Items;
            set => GetOrCreateFeature(_newItemsFeature).Items = value;
        }

        public override IServiceProvider ConnectionServices
        {
            get => GetOrCreateFeature(_newServiceProvidersFeature).RequestServices;
            set => GetOrCreateFeature(_newServiceProvidersFeature).RequestServices = value;
        }

        public override CancellationToken ConnectionAborted
        {
            get => GetOrCreateFeature(_newRequestLifetimeFeature).RequestAborted;
            set => GetOrCreateFeature(_newRequestLifetimeFeature).RequestAborted = value;
        }

        public DefaultConnectionContext() : this(new FeatureCollection()) { }

        public DefaultConnectionContext(IFeatureCollection features)
        {
            _features = features;
        }

        public override void Abort() => GetOrCreateFeature(_newRequestLifetimeFeature).Abort();

        public override bool TryGetChannel(out IChannelConnection<Message> channel)
        {
            var feature = _features.Get<IConnectionChannelFeature>();
            if(feature != null)
            {
                channel = feature.Channel;
                return true;
            }
            else
            {
                channel = null;
                return false;
            }
        }

        public override bool TryGetPipe(out IPipeConnection pipe)
        {
            var feature = _features.Get<IConnectionPipeFeature>();
            if(feature != null)
            {
                pipe = feature.Pipe;
                return true;
            }
            else
            {
                pipe = null;
                return false;
            }
        }

        private T GetOrCreateFeature<T>(Func<T> constructor)
        {
            var feature = _features.Get<T>();
            if (feature == null)
            {
                feature = constructor();
                _features.Set(feature);
            }
            return feature;
        }
    }
}
