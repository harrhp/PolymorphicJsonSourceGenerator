using Nuke.Common.IO;
using static Nuke.Common.NukeBuild;

namespace _build.Components;

public interface ArtifactsComponent
{
    static AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
}
