using Nuke.Common.IO;
using static Nuke.Common.NukeBuild;

namespace _build.Components;

public interface ChangelogComponent
{
    static AbsolutePath ChangelogFile => RootDirectory / "CHANGELOG.md";
}
