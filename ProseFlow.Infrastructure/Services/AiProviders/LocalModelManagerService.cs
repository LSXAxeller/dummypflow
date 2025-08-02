using LLama;
using LLama.Batched;
using LLama.Common;
using Microsoft.Extensions.Logging;
using ProseFlow.Core.Models;
using Action = System.Action;

namespace ProseFlow.Infrastructure.Services.AiProviders;

public enum ModelStatus { NotLoaded, Loading, Loaded, Error }

/// <summary>
/// Manages the lifecycle of the local LLM, including loading and unloading the model.
/// This service is a singleton to ensure the model is only loaded into memory once.
/// It has no dependencies on any UI framework.
/// </summary>
public class LocalModelManagerService(ILogger<LocalModelManagerService> logger)
{
    // Event for the UI to subscribe to for state changes.
    public event Action? StateChanged;

    public ModelStatus Status { get; private set; } = ModelStatus.NotLoaded;
    public string? ErrorMessage { get; private set; }

    public LLamaWeights? Model { get; private set; }
    public BatchedExecutor? Executor { get; private set; }

    public bool IsLoaded => Status == ModelStatus.Loaded;

    /// <summary>
    /// Loads the local model into memory based on the provided settings.
    /// This operation is expensive and should only be done when necessary.
    /// </summary>
    /// <param name="settings">The provider settings containing the model path and parameters.</param>
    public async Task LoadModelAsync(ProviderSettings settings)
    {
        if (Status is ModelStatus.Loading or ModelStatus.Loaded)
        {
            logger.LogInformation("Model load requested, but it's already loading or loaded.");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.LocalModelPath) || !File.Exists(settings.LocalModelPath))
        {
            UpdateState(ModelStatus.Error, "Model path is not set or the file does not exist.");
            logger.LogError(ErrorMessage);
            return;
        }

        try
        {
            UpdateState(ModelStatus.Loading);
            
            var modelParams = new ModelParams(settings.LocalModelPath)
            {
                ContextSize = (uint)(settings.LocalModelContextSize > 0 ? settings.LocalModelContextSize : 4096),
                GpuLayerCount = settings.PreferGpu ? 99 : 0,
                Threads = settings.LocalCpuCores > 0 ? settings.LocalCpuCores : null,
                UseMemorymap = settings.LocalModelMemoryMap,
                UseMemoryLock = settings.LocalModelMemorylock
            };
            
            Model = await LLamaWeights.LoadFromFileAsync(modelParams);
            Executor = new BatchedExecutor(Model, modelParams);

            UpdateState(ModelStatus.Loaded);
            logger.LogInformation("Successfully loaded local model from: {Path}", settings.LocalModelPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load local model.");
            var errorMessage = $"Failed to load model: {ex.Message}";
            UpdateState(ModelStatus.Error, errorMessage);
            UnloadModel(); // Clean up any partially loaded resources
        }
    }

    /// <summary>
    /// Unloads the model from memory and frees associated resources.
    /// </summary>
    public void UnloadModel()
    {
        logger.LogInformation("Unloading local model.");
        Executor?.Dispose();
        Model?.Dispose();
        
        Executor = null;
        Model = null;
        
        UpdateState(ModelStatus.NotLoaded);

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
    
    /// <summary>
    /// Centralized method to update the service's state and notify subscribers.
    /// </summary>
    private void UpdateState(ModelStatus newStatus, string? errorMessage = null)
    {
        Status = newStatus;
        ErrorMessage = errorMessage;
        StateChanged?.Invoke();
    }
}