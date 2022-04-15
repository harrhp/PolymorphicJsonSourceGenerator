using _build.Components;
using Nuke.Common;

namespace _build;

internal class Build : NukeBuild, ReleaseComponent
{
    public static int Main() => Execute<Build>();

    protected override void OnBuildCreated()
    {
        base.OnBuildCreated();
        NoLogo = true;
    }
}
