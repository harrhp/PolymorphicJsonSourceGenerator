using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace _build.Components;

public interface TestComponent : BuildComponent
{
    Target Test =>
        _ => _.DependsOn(Build)
            .Executes(() => DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)));
}
