using _build.Extensions;
using Nuke.Common;
using Nuke.Common.Git;

namespace _build.Components;

public interface RepositoryComponent : INukeBuild
{
    [GitRepository]
    GitRepository Repository => this.GetValue(() => Repository);
}
