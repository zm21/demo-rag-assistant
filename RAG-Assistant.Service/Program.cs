using Microsoft.AspNetCore.Mvc;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.AI.OpenAI;
using Microsoft.KernelMemory.Service.AspNetCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

#pragma warning disable SKEXP0070

var builder = WebApplication.CreateBuilder(args);


#region Configure Services

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

#endregion

#region Constants

const string textGeneratingModel = "phi3.5:latest";
const string embeddingModel = "nomic-embed-text:latest";

#endregion

#region Adds OpenTelemetry and health checks for Postgres

builder.AddNpgsqlDataSource("rag-db");

#endregion

#region Retrieve LLMs Configuration

// Retrieve Ollama API endpoints from configuration
var ollamaOpenApiEndpoint = builder.Configuration.GetConnectionString("phi");
var ollamaEmbeddingApiEndpoint = builder.Configuration.GetConnectionString("nomic");

if (ollamaOpenApiEndpoint == null || ollamaEmbeddingApiEndpoint == null)
    throw new NullReferenceException("OllamaOpenApiEndpointUri is null");

#endregion

#region Configure Semantic Kernel and Ollama Chat Completion

// Add Semantic Kernel and configure Ollama Chat Completion service
builder
    .Services.AddKernel()
    .AddOllamaChatCompletion(
        modelId: textGeneratingModel,
        endpoint: new Uri(ollamaOpenApiEndpoint)
    );

#endregion

#region Configure Ollama

var textOllamaConfig = new OllamaConfig
{
    Endpoint = ollamaOpenApiEndpoint,
    TextModel = new OllamaModelConfig(textGeneratingModel, 131072), // Text model with context size
};

var embeddingOllamaConfig = new OllamaConfig
{
    Endpoint = ollamaEmbeddingApiEndpoint,
    EmbeddingModel = new OllamaModelConfig(embeddingModel, 2048) // Embedding model with context size
};

#endregion

#region Configure Kernel Memory

builder.Services.AddKernelMemory<MemoryServerless>(memoryBuilder =>
{
    memoryBuilder
        .WithPostgresMemoryDb(
            new PostgresConfig()
            {
                ConnectionString = builder.Configuration.GetConnectionString("rag-db")!
            }
        )
        .WithOllamaTextGeneration(
            textOllamaConfig,
            new GPT4oTokenizer()
        )
        .WithOllamaTextEmbeddingGeneration(
            embeddingOllamaConfig,
            new GPT4oTokenizer()
        );
});

#endregion

var app = builder.Build();

#region Configure Endpoints

app.MapDefaultEndpoints();

app.AddKernelMemoryEndpoints(apiPrefix: "/rag");

#endregion

#region Define Custom Endpoint

app.MapGet(
    "/rag/my-query",
    async ([FromQuery] string q, Kernel kernel) =>
    {
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        var result = await chatCompletionService.GetChatMessageContentAsync(
            q,
            kernel: kernel
        );

        return new { result };
    }
);

#endregion

#region Configure Swagger and Run Application

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

app.Run();

#endregion
