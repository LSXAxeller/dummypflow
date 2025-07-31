namespace ProseFlow.Core.Models;

/// <summary>
/// Stores settings related to AI providers, intended to be a single record in the database.
/// </summary>
public class ProviderSettings
{
    public int Id { get; set; }

    #region Cloud Settings
    
    /// <summary>
    /// The user's API key for the Cloud service.
    /// </summary>
    public string CloudApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// The base URL for the Cloud service.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>
    /// The specific Cloud model to use (e.g., "gpt-4o", "gpt-3.5-turbo").
    /// </summary>
    public string CloudModel { get; set; } = "gpt-4o";

    /// <summary>
    /// The temperature setting for Cloud, controlling randomness (0.0 to 2.0).
    /// </summary>
    public float CloudTemperature { get; set; } = 0.7f;
    
    #endregion
    
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
    /// The name of the primary provider to use for requests ("Cloud" or "Local").
    /// </summary>
    public string PrimaryProvider { get; set; } = nameof(Enums.ProviderType.Cloud);

    /// <summary>
    /// The name of the fallback provider to use if the primary one fails ("Cloud", "Local", or "None").
    /// </summary>
    public string FallbackProvider { get; set; } = nameof(Enums.ProviderType.None);
    
    #endregion
}