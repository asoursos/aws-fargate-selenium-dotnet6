namespace Cdk;

public interface IEnvironmentProps
{
    string Environment { get; }
}

public record EnvironmentProps(string Environment) : IEnvironmentProps;

public static class Naming
{
    public static string Name(this IEnvironmentProps env, string name) => $"sel-{name}-{env.Environment}";
}
