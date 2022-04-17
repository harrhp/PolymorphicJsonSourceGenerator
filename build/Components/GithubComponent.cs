using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Tools.Git;

namespace _build.Components;

public interface GithubComponent : INukeBuild
{
    Target ConfigureGit =>
        _ => _.Unlisted()
            .OnlyWhenStatic(() => GitHubActions.Instance != null)
            .Executes(() =>
                GitTasks.Git($"config --global user.name {Environment.GetEnvironmentVariable("GITHUB_ACTOR")}"));
}
