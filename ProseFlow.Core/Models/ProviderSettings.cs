namespace ProseFlow.Core.Models;

/// <summary>
/// Stores settings related to AI providers, handling Local LLM settings and the global service type choice.
/// </summary>
public class ProviderSettings
{
    public int Id { get; set; }
    
    #region Local Model Settings
    
    /// <summary>
    /// The file path to the local model file (e.g., a .gguf file).
    /// </summary>
    public string LocalModelPath { get; set; } = string.Empty;

    /// <summary>
    /// The number of CPU cores to allocate for local inference.
    /// </summary>
    public int LocalCpuCores { get; set; } = 4;

    /// <summary>
    /// If true, the application will attempt to use GPU acceleration for local models if available.
    /// </summary>
    public bool PreferGpu { get; set; } = true;
    
    #endregion
    
    #region Provider Switching Logic

    /// <summary>
    /// The primary service type to use for requests ("Cloud" or "Local").
    /// </summary>
    public string PrimaryServiceType { get; set; } = "Cloud";

    /// <summary>
    /// The fallback service type to use if the primary one fails ("Cloud", "Local", or "None").
    /// </summary>
    public string FallbackServiceType { get; set; } = "None";
    
    #endregion
}