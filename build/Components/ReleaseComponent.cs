using NuGet.Versioning;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.GitHub;
using Nuke.Common.Utilities;
using Octokit;
using static Nuke.Common.ChangeLog.ChangelogTasks;
using static Nuke.Common.Tools.Git.GitTasks;

namespace _build.Components;

public interface ReleaseComponent : PublishComponent, ChangelogComponent, RepositoryComponent
{
    string Version =>
        Solution.AllProjects.First(x => !string.IsNullOrEmpty(x.GetProperty("PackageId")))
            .GetProperty("Version")
            .NotNull();

    string Tag => $"v{Version}";

    [Parameter, Secret]
    string? GithubToken => TryGetValue(() => GithubToken) ?? GitHubActions.Instance?.Token;

    Target Changelog =>
        _ => _.Unlisted()
            .After(Publish)
            .Requires(() => GitHasCleanWorkingCopy())
            .Requires(() => File.Exists(ChangelogFile))
            .Executes(() =>
            {
                FinalizeChangelog(ChangelogFile, Version, Repository);
                Git($"add {ChangelogFile}");
                Git($"commit -m \"Finalize {Path.GetFileName(ChangelogFile)} for {Version}\"");
            });

    Target Release =>
        _ => _.DependsOn(Changelog)
            .Executes(() =>
            {
                var branch = GitCurrentBranch();
                Git($"tag {Tag}");
                Git($"push --atomic origin {branch} {Tag}");
            });

    Target CreateGithubRelease =>
        _ => _.Requires(() => GithubToken)
            .OnlyWhenStatic(() => Repository.IsGitHubRepository())
            .After(Release)
            .Executes(async () =>
            {
                var githubClient = new GitHubClient(new ProductHeaderValue(nameof(NukeBuild)))
                {
                    Credentials = new Credentials(GithubToken)
                };

                await githubClient.Repository.Release.Create(
                    Repository.GetGitHubOwner(),
                    Repository.GetGitHubName(),
                    new NewRelease(Tag)
                    {
                        Name = Tag,
                        Body = ExtractChangelogSectionNotes(ChangelogFile, Version).JoinNewLine(),
                        Prerelease = NuGetVersion.Parse(Version).IsPrerelease
                    });
            });
}
