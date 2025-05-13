using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using FirstPlugin;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Assistants;
using OpenAI.Chat;
using Microsoft.SemanticKernel.Plugins.Web.Brave;
using Microsoft.SemanticKernel.Plugins.Web;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_ApiKey");
var endpoint = Environment.GetEnvironmentVariable("OPENAI_API_Endpoint");
var apiKeyBrave = Environment.GetEnvironmentVariable("BRAVE_API_KEY");

// Create and configure the kernel
var builder = Kernel.CreateBuilder();
builder.AddAzureOpenAIChatCompletion(deploymentName: "gpt-4o-mini",
                endpoint: endpoint!,
                apiKey: apiKey!);

var kernel = builder.Build();

var braveConnector = new BraveConnector(apiKeyBrave!);
var bravePlugin = new WebSearchEnginePlugin(braveConnector);

ChatCompletionAgent agent = CreateAgentWithPlugin(
                plugins: [
                    KernelPluginFactory.CreateFromType<LightsPlugin>(), 
                    KernelPluginFactory.CreateFromType<SoftwareBuilderPlugin>(), 
                    KernelPluginFactory.CreateFromType<GoogleKeepPlugin>(),
                    KernelPluginFactory.CreateFromType<GoogleFitnessPlugin>(),
                    KernelPluginFactory.CreateFromObject(bravePlugin),
                ],
                instructions: "Respond to user questions as an assistant",
                name: "Main-Assistant");

AgentThread thread = new ChatHistoryAgentThread();

// Respond to user input, invoking functions where appropriate.
while (true)
{
    await InvokeAgentAsync(agent, thread);
}

bool FunctionCallsPresent(Microsoft.SemanticKernel.ChatMessageContent message)
{
    // Get function calls from the chat message content and quit the chat loop if no function calls are found.
    IEnumerable<FunctionCallContent> functionCalls = FunctionCallContent.GetFunctionCalls(message);
    if (functionCalls.Any())
    {
        return true;
    }
    return false;
}

static IEnumerable<FunctionResultContent> GetFunctionCalls(Microsoft.SemanticKernel.ChatMessageContent messageContent)
{
    return messageContent.Items.OfType<FunctionResultContent>();
}

ChatCompletionAgent CreateAgentWithPlugin(
        IEnumerable<KernelPlugin> plugins,
        string? instructions = null,
        string? name = null)
{
    ChatCompletionAgent agent =
            new()
            {
                Instructions = instructions,
                Name = name,
                Kernel = kernel,
                Arguments = new KernelArguments(new PromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
            };

    // Initialize plugin and add to the agent's Kernel (same as direct Kernel usage).
    agent.Kernel.Plugins.AddRange(plugins);

    return agent;
}

async Task InvokeAgentAsync(ChatCompletionAgent agent, AgentThread thread)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"User input:");
    var input = Console.ReadLine();
    Console.ResetColor();
    
    // Check if user wants to clear context
    if (input?.ToLower() == "clear-context")
    {
        thread = new ChatHistoryAgentThread();
        Console.WriteLine("Conversation context has been cleared.");
    }

    Microsoft.SemanticKernel.ChatMessageContent message = new(AuthorRole.User, input);

    try
    {
        await foreach (var response in agent.InvokeAsync(message, thread))
        {
            WriteFunctionCalls(response);
            WriteAgentChatMessage(response);
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error invoking chat completion: {ex.Message}");
        Console.ResetColor();
        return;
    }
}

void WriteFunctionCalls(AgentResponseItem<Microsoft.SemanticKernel.ChatMessageContent> response)
{
    var functionCallResults = GetFunctionCalls(response.Message);
    foreach (var functionResult in functionCallResults)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Function call: {functionResult?.PluginName} - {functionResult?.FunctionName}");
        
        // Display function call details
        Console.WriteLine($"  Call ID: {functionResult?.CallId}");
        Console.WriteLine($"  Plugin Name: {functionResult?.PluginName}");
        Console.WriteLine($"  Function Name: {functionResult?.FunctionName}");
        Console.ResetColor();

        // Check if the function call has a result and display it with more context
        if (functionResult?.Result != null)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  Function result: {functionResult.Result}");
            
            // Try to extract more detailed information about the result
            try
            {
                var resultType = functionResult.Result.GetType().Name;
                Console.WriteLine($"  Result Type: {resultType}");
                
                // For complex objects like collections, provide more detail
                if (functionResult.Result is System.Collections.IEnumerable collection && 
                    !(functionResult.Result is string))
                {
                    int itemCount = 0;
                    foreach (var item in collection)
                    {
                        itemCount++;
                        if (itemCount <= 3) // Limit to first 3 items to avoid excessive output
                        {
                            Console.WriteLine($"    Item {itemCount}: {item}");
                        }
                    }
                    
                    if (itemCount > 3)
                    {
                        Console.WriteLine($"    ... and {itemCount - 3} more items");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  (Error parsing result details: {ex.Message})");
            }
            
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("  No result returned");
            Console.ResetColor();
        }
        
        Console.WriteLine(); // Add spacing between different function calls
    }

    if (!functionCallResults.Any())
    {
        Console.WriteLine("No function calls found.");
    }
}

void WriteAgentChatMessage(Microsoft.SemanticKernel.ChatMessageContent message)
{
    Console.ForegroundColor = ConsoleColor.Green;
    
    // Include ChatMessageContent.AuthorName in output, if present.
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    string authorExpression = message.Role == AuthorRole.User ? string.Empty : $" - {message.AuthorName ?? "*"}";
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    
    string contentExpression = string.IsNullOrWhiteSpace(message.Content) ? string.Empty : message.Content;
    Console.WriteLine($"\n# {message.Role}{authorExpression}: {contentExpression}");

    // Provide visibility for inner content (that isn't TextContent).
    foreach (KernelContent item in message.Items)
    {
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        if (item is AnnotationContent annotation)
        {
            Console.WriteLine($"  [{item.GetType().Name}] {annotation.Quote}: File #{annotation.FileId}");
        }
        else if (item is FileReferenceContent fileReference)
        {
            Console.WriteLine($"  [{item.GetType().Name}] File #{fileReference.FileId}");
        }
        else if (item is ImageContent image)
        {
            Console.WriteLine($"  [{item.GetType().Name}] {image.Uri?.ToString() ?? image.DataUri ?? $"{image.Data?.Length} bytes"}");
        }
        else if (item is FunctionCallContent functionCall)
        {
            Console.WriteLine($" Function call content: [{item.GetType().Name}] {functionCall.Id}");
        }
        else if (item is FunctionResultContent functionResult)
        {
            Console.WriteLine($" Function result content: [{item.GetType().Name}] {functionResult.CallId} - {functionResult.Result?.ToString() ?? "*"}");
        }
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    if (message.Metadata?.TryGetValue("Usage", out object? usage) ?? false)
    {
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        if (usage is RunStepTokenUsage assistantUsage)
        {
            WriteUsage(assistantUsage.TotalTokenCount, assistantUsage.InputTokenCount, assistantUsage.OutputTokenCount);
        }
        else if (usage is ChatTokenUsage chatUsage)
        {
            WriteUsage(chatUsage.TotalTokenCount, chatUsage.InputTokenCount, chatUsage.OutputTokenCount);
        }
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }
    
    Console.ResetColor();

    void WriteUsage(long totalTokens, long inputTokens, long outputTokens)
    {
        Console.WriteLine($"  [Usage] Tokens: {totalTokens}, Input: {inputTokens}, Output: {outputTokens}");
    }
}