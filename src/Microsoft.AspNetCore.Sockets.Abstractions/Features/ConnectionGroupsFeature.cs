using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNetCore.Sockets.Features
{
    public class ConnectionGroupsFeature : IConnectionGroupsFeature
    {
        private object _lock = new object();
        private HashSet<string> _groups = new HashSet<string>();

        public IEnumerable<string> GetGroups()
        {
            lock(_lock)
            {
                // Must use ToList to ensure we copy the values before we unlock
                return _groups.ToList();
            }
        }

        public bool AddGroup(string group)
        {
            lock(_lock)
            {
                return _groups.Add(group);
            }
        }

        public bool IsInGroup(string group)
        {
            lock(_lock)
            {
                return _groups.Contains(group);
            }
        }

        public bool RemoveGroup(string group)
        {
            lock(_lock)
            {
                return _groups.Remove(group);
            }
        }
    }

    public static class ConnectionGroupsConnectionContextExtensions
    {
        private static IConnectionGroupsFeature GetOrCreateFeature(ConnectionContext context)
        {
            var feature = context.Features.Get<IConnectionGroupsFeature>();
            if(feature == null)
            {
                feature = new ConnectionGroupsFeature();
                context.Features.Set(feature);
            }
            return feature;
        }

        // These two have to create the feature to work
        public static bool AddGroup(this ConnectionContext context, string groupName) => GetOrCreateFeature(context).AddGroup(groupName);
        public static bool RemoveGroup(this ConnectionContext context, string groupName) => GetOrCreateFeature(context).RemoveGroup(groupName);

        // These two can just return default values (false/empty enumerable) if the feature isn't present
        public static bool IsInGroup(this ConnectionContext context, string groupName) => context.Features.Get<IConnectionGroupsFeature>()?.IsInGroup(groupName) ?? false;
        public static IEnumerable<string> GetGroups(this ConnectionContext context) => context.Features.Get<IConnectionGroupsFeature>()?.GetGroups() ?? Enumerable.Empty<string>();
    }
}
