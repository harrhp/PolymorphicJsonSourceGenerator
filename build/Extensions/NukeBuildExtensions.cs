using System.Linq.Expressions;
using Nuke.Common;
using Nuke.Common.Utilities;

namespace _build.Extensions;

internal static class NukeBuildExtensions
{
    public static T GetValue<T>(this INukeBuild build, Expression<Func<T>> expression) where T : class =>
        build.TryGetValue(expression).NotNull($"Parameter {expression.GetMemberInfo().Name} must be set");
}
