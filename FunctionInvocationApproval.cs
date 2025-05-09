using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// Service that verifies if function invocation is approved.
/// </summary>
public interface IFunctionApprovalService
{
    bool IsInvocationApproved(KernelFunction function, KernelArguments arguments);
}

public sealed class ConsoleFunctionApprovalService : IFunctionApprovalService
{
    public bool IsInvocationApproved(KernelFunction function, KernelArguments arguments)
    {
        Console.WriteLine("====================");
        Console.WriteLine($"Function name: {function.Name}");
        Console.WriteLine($"Plugin name: {function.PluginName ?? "N/A"}");

        if (arguments.Count == 0)
        {
            Console.WriteLine("\nArguments: N/A");
        }
        else
        {
            Console.WriteLine("\nArguments:");

            foreach (var argument in arguments)
            {
                Console.WriteLine($"{argument.Key}: {argument.Value}");
            }
        }

        Console.WriteLine("\nApprove invocation? (yes/no)");

        var input = Console.ReadLine();

        return input?.Equals("yes", StringComparison.OrdinalIgnoreCase) ?? false;
    }
}

/// <summary>
/// Filter to invoke function only if it's approved.
/// </summary>
public sealed class FunctionInvocationFilter(IFunctionApprovalService approvalService) : IFunctionInvocationFilter
{
    private readonly IFunctionApprovalService _approvalService = approvalService;

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        // Invoke the function only if it's approved.
        if (_approvalService.IsInvocationApproved(context.Function, context.Arguments))
        {
            await next(context);
        }
        else
        {
            // Otherwise, return a result that operation was rejected.
            context.Result = new FunctionResult(context.Result, "Operation was rejected.");
        }
    }
}

#region Plugins

[Description("Software Builder Plugin")]
public sealed class SoftwareBuilderPlugin
{
    [KernelFunction]
    [Description("Collects requirements for the software project.")]
    public string CollectRequirements()
    {
        Console.WriteLine("Collecting requirements...");
        return "Requirements";
    }

    [KernelFunction]
    [Description("Designs the software based on the requirements.")]
    public string Design(string requirements)
    {
        Console.WriteLine($"Designing based on: {requirements}");
        return "Design";
    }

    [KernelFunction]
    [Description("Implements the software based on the requirements and design.")]
    public string Implement(string requirements, string design)
    {
        Console.WriteLine($"Implementing based on {requirements} and {design}");
        return "Implementation";
    }

    [KernelFunction]
    [Description("Tests the software based on the requirements, design, and implementation.")]
    public string Test(string requirements, string design, string implementation)
    {
        Console.WriteLine($"Testing based on {requirements}, {design} and {implementation}");
        return "Test Results";
    }

    [KernelFunction]
    [Description("Deploys the software based on the requirements, design, implementation, and test results.")]
    public string Deploy(string requirements, string design, string implementation, string testResults)
    {
        Console.WriteLine($"Deploying based on {requirements}, {design}, {implementation} and {testResults}");
        return "Deployment";
    }
}

#endregion

class FunctionInvocationApproval
{
    public static async Task ExecuteAsync(IKernelBuilder builder)
    {
        // Add function approval service and filter
        builder.Services.AddSingleton<IFunctionApprovalService, ConsoleFunctionApprovalService>();
        builder.Services.AddSingleton<IFunctionInvocationFilter, FunctionInvocationFilter>();

        // Add software builder plugin
        builder.Plugins.AddFromType<SoftwareBuilderPlugin>();

        var kernel = builder.Build();

        // Enable automatic function calling
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        // Initialize kernel arguments.
        var arguments = new KernelArguments(executionSettings);

        // Start execution
        // Try to reject invocation at each stage to compare LLM results.
        var result = await kernel.InvokePromptAsync("I want to build a software. Let's start from the first step. Continue unitll all steps were executed or one of the steps are rejected.", arguments);

        Console.WriteLine(result);
    }
}