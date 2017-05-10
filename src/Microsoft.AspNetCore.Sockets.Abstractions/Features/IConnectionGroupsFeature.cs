using System.Collections.Generic;

namespace Microsoft.AspNetCore.Sockets.Features
{
    public interface IConnectionGroupsFeature
    {
        bool AddGroup(string group);
        bool RemoveGroup(string group);
        bool IsInGroup(string group);
        IEnumerable<string> GetGroups();
    }
}
