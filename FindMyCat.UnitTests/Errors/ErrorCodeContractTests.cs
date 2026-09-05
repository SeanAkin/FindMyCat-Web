using System.Text.RegularExpressions;
using FindMyCat.Core.Errors;

namespace FindMyCat.UnitTests.Errors;

public sealed class ErrorCodeContractTests
{
    private static readonly string TypeScriptTypesPath =
        Path.Combine(RepositoryRoot(), "frontend", "src", "api", "types.ts");

    [Fact]
    public void Every_backend_code_is_declared_in_the_frontend_union()
    {
        var declaredInFrontend = ApiErrorCodeUnionMembers();

        var missing = ErrorCodes.All.Where(code => !declaredInFrontend.Contains(code)).ToList();

        missing.ShouldBeEmpty(
            $"These codes exist in {nameof(ErrorCodes)} but not in the frontend ApiErrorCode union: {string.Join(", ", missing)}.");
    }

    [Fact]
    public void Every_frontend_union_member_is_declared_in_the_backend()
    {
        var declaredInBackend = ErrorCodes.All.ToHashSet();

        var extra = ApiErrorCodeUnionMembers().Where(code => !declaredInBackend.Contains(code)).ToList();

        extra.ShouldBeEmpty(
            $"These codes exist in the frontend ApiErrorCode union but not in {nameof(ErrorCodes)}: {string.Join(", ", extra)}.");
    }

    [Fact]
    public void Codes_are_unique()
    {
        ErrorCodes.All.Distinct().Count().ShouldBe(ErrorCodes.All.Count);
    }

    private static HashSet<string> ApiErrorCodeUnionMembers()
    {
        var source = File.ReadAllText(TypeScriptTypesPath);

        var union = Regex.Match(source, @"export type ApiErrorCode =(?<body>[\s\S]*?)(?=\r?\n\r?\n|\z)");
        union.Success.ShouldBeTrue($"Could not find the ApiErrorCode union in {TypeScriptTypesPath}.");

        return Regex.Matches(union.Groups["body"].Value, @"'(?<code>[a-z0-9_]+)'")
            .Select(match => match.Groups["code"].Value)
            .ToHashSet();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FindMyCat.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("Could not locate the repository root from the test output directory.");
        return directory.FullName;
    }
}
