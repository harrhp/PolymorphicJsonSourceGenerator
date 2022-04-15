using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using static _build.Components.ArtifactsComponent;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace _build.Components;

public interface BuildComponent : SolutionComponent
{
    static string Configuration => "Release";

    Target Restore => _ => _.Executes(() => DotNetRestore(s => s.SetProjectFile(Solution)));

    Target Build =>
        _ => _.DependsOn(Restore)
            .Executes(() => DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()));
}
