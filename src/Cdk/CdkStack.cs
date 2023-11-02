using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Constructs;
using System.Collections.Generic;
using System.IO;

namespace Cdk;

public class CdkStack : Stack
{
    internal CdkStack(Construct scope, string id, IStackProps props = null, IEnvironmentProps environmentProps = null) : base(scope, id, props)
    {
        var env = environmentProps ?? new EnvironmentProps("development");

        var vpc = BuildVpc(env);
        Role taskRole = BuildTaskRole(env);
        Role executionRole = BuildExecutionRole(env);

        var taskProps = new FargateTaskProps(vpc, env, taskRole, executionRole, "../ChromeStandalone");
        var taskDefinition = BuildTaskDefinition(env, taskProps);
        var container = BuildContainer(taskProps, taskDefinition);
        var cluster = BuildCluster(taskProps);
        var securityGroup = BuildSecurityGroup(taskProps);
    }

    #region [ VPC ]
    private Vpc BuildVpc(IEnvironmentProps env)
    {
        var vpc = new Vpc(this, env.Name("selenium-vpc"), new VpcProps
        {
            EnableDnsHostnames = true,
            EnableDnsSupport = true,
            MaxAzs = 1,
            NatGateways = 0,
            SubnetConfiguration = new[]
            {
                new SubnetConfiguration
                {
                    Name = "public",
                    SubnetType = SubnetType.PUBLIC,
                    CidrMask = 24
                }
            }
        });

        return vpc;
    }
    #endregion

    #region [ Roles ]
    private Role BuildExecutionRole(IEnvironmentProps env)
    {
        // build fargate task execution role
        var role = new Role(this,
            env.Name("scraping-execution-role"),
            new RoleProps
            {
                RoleName = env.Name("selenium-execution-role"),
                AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com")
            });
        role.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("service-role/AmazonECSTaskExecutionRolePolicy"));
        role.AddToPolicy(new(
            new PolicyStatementProps
            {
                Sid = "AllowExecutionRoleCloudWatch",
                Effect = Effect.ALLOW,
                Resources = new[]
                {
                "*"
                },
                Actions = new[]
                {
                    // https://github.com/aws/aws-logging-dotnet#required-iam-permissions
                    "logs:CreateLogGroup",
                    "logs:DescribeLogGroups",
                }
            }));

        return role;
    }

    private Role BuildTaskRole(IEnvironmentProps env)
    {
        var name = env.Name("selenium-task-role");
        var role = new Role(this, name,
        new RoleProps
        {
            RoleName = name,
            AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com")
        });

        role.AddToPolicy(new(
            new PolicyStatementProps
            {
                Sid = "AllowTaskRoleCloudWatch",
                Effect = Effect.ALLOW,
                Resources = new[]
                {
                "*"
                },
                Actions = new[]
                {
                    // https://github.com/aws/aws-logging-dotnet#required-iam-permissions
                    "logs:CreateLogGroup",
                    "logs:CreateLogStream",
                    "logs:PutLogEvents",
                    "logs:DescribeLogGroups",
                }
            }));

        return role;
    }
    #endregion

    #region [ Task ]
    public record FargateTaskProps(IVpc Vpc,
    IEnvironmentProps Environment,
    Amazon.CDK.AWS.IAM.Role TaskRole,
    Amazon.CDK.AWS.IAM.Role ExecutionRole,
    string DockerDirectory);

    public TaskDefinition BuildTaskDefinition(IEnvironmentProps env, FargateTaskProps taskProps)
    {
        var path = Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, taskProps.DockerDirectory));
        var dir = new DirectoryInfo(path);

        if (dir.Exists == false)
        {
            throw new System.ArgumentException($"{System.Environment.CurrentDirectory}, {path}, {taskProps.DockerDirectory}, {nameof(taskProps.DockerDirectory)}");
        }

        // create task definition.
        // https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task-cpu-memory-error.html
        return new FargateTaskDefinition(this, taskProps.Environment.Name("selenium-task-def"), new FargateTaskDefinitionProps
        {
            ExecutionRole = taskProps.ExecutionRole,
            TaskRole = taskProps.TaskRole,
            Cpu = 512,
            MemoryLimitMiB = 1024,
            
        });
    }

    private Cluster BuildCluster(FargateTaskProps props)
    {
        var cluster = new Cluster(this, props.Environment.Name("selenium-cluster"), new ClusterProps
        {
            ClusterName = props.Environment.Name("selenium-cluster"),
            Vpc = props.Vpc,
            ContainerInsights = true,
        });

        return cluster;
    }

    private ContainerDefinition BuildContainer(FargateTaskProps props, TaskDefinition taskDefinition)
    {
        var logGroupName = props.Environment.Name($"selenium-log-group");
        var logDriver = new AwsLogDriver(new AwsLogDriverProps
        {
            StreamPrefix = props.Environment.Name($"selenium-log-stream"),
            LogGroup = new LogGroup(this, props.Environment.Name($"selenium-log-group-id"), new LogGroupProps
            {

                LogGroupName = logGroupName,
                RemovalPolicy = RemovalPolicy.DESTROY,
                Retention = RetentionDays.ONE_WEEK
            })
        });

        var env = new Dictionary<string, string>
            {
                { "LOG_GROUP", logGroupName },
                { "AWS_REGION", props.Vpc.Stack.Region }
            };

        var appContainer = taskDefinition.AddContainer(props.Environment.Name($"selenium-task-container"), new ContainerDefinitionOptions
        {
            Image = ContainerImage.FromAsset(props.DockerDirectory),
            ContainerName = props.Environment.Name($"selenium-container"),
            Essential = true,
            Logging = logDriver,
            Environment = env
        });

        return appContainer;
    }
    #endregion

    #region [ Security ]
    private SecurityGroup BuildSecurityGroup(FargateTaskProps props)
    {
        var securityGroup = new SecurityGroup(this, props.Environment.Name("selenium-sg"), new SecurityGroupProps
        {
            Vpc = props.Vpc,
            AllowAllOutbound = true // Allow outbound traffic
        });

        return securityGroup;
    }
    #endregion
}
