namespace Telechron.Sdk.Modules;

// R-MOD4: the unified self-test contract's result shape -- every module
// reports through this same type regardless of what it internally tests.
public sealed record ModuleSelfTestResult(bool Passed, string Summary, IReadOnlyList<string> Errors)
{
    public static ModuleSelfTestResult Success(string summary) => new(true, summary, []);
    public static ModuleSelfTestResult Failure(string summary, params string[] errors) => new(false, summary, errors);
}
