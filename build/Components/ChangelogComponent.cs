using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tools.Git;
using Octokit;
using Serilog;
using static Nuke.Common.ChangeLog.ChangelogTasks;
using static Nuke.Common.Tools.Git.GitTasks;

namespace _build.Components;

[ParameterPrefix("Changelog")]
public interface ChangelogComponent : RepositoryComponent, VersionComponent, PublishComponent
{
    AbsolutePath ChangelogFile => RootDirectory / "CHANGELOG.md";

    Target Changelog =>
        _ => _.TryDependsOn<GithubComponent>(x => x.ConfigureGit)
            .Requires(() => Repository.IsOnMainBranch())
            .Requires(() => GitHasCleanWorkingCopy())
            .Requires(() => File.Exists(ChangelogFile))
            .After(Publish)
            .Executes(() =>
            {
                FinalizeChangelog(ChangelogFile, Version, Repository);
                Git($"add {ChangelogFile}");
                Git($"commit -m \"Finalize {Path.GetFileName(ChangelogFile)} for {Version}\"");
            });
}
