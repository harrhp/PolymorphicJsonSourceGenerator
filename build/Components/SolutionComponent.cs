using _build.Extensions;
using Nuke.Common;
using Nuke.Common.ProjectModel;

namespace _build.Components;

public interface SolutionComponent : INukeBuild
{
    [Solution]
    Solution Solution => this.GetValue(() => Solution);
}
