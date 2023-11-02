using Amazon.CDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cdk;

sealed class Program
{
    public static void Main(string[] args)
    {
        var app = new App();
        new CdkStack(app, "CdkStack", GetStackProps(), GetEnvProps());
        app.Synth();
    }

    private static StackProps GetStackProps()
    {
        return new StackProps
        {
            Env = new Amazon.CDK.Environment
            {
                Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION"),
            }
        };
    }

    public static IEnvironmentProps GetEnvProps()
    {
        var environmentName = System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "development"; // "production
        return new EnvironmentProps(environmentName);
    }
}
