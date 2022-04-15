using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using static _build.Components.ArtifactsComponent;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace _build.Components;

public interface PackComponent : BuildComponent
{
    AbsolutePath PackagesDirectory => ArtifactsDirectory / "packages";

    Target Pack =>
        _ => _.DependsOn(Build)
            .Executes(() =>
            {
                EnsureCleanDirectory(PackagesDirectory);
                DotNetPack(s => s
                    .SetProject(Solution)
                    .SetConfiguration(Configuration)
                    .EnableNoBuild()
                    .SetOutputDirectory(PackagesDirectory));
            });
}
