namespace ProseFlow.Core.Models;

/// <summary>
/// Represents a user-defined AI task or "Action".
/// </summary>
public class Action
{
    public int Id { get; set; }

    /// <summary>
    /// The unique, user-facing name of the action (e.g., "Proofread", "Summarize").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// A short phrase prepended to the user's selected text before sending to the AI.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// The core set of rules and guidelines (system prompt) for the AI.
    /// </summary>
    public string Instruction { get; set; } = string.Empty;

    /// <summary>
    /// The resource path or identifier for the action's icon.
    /// </summary>
    public string Icon { get; set; } = "avares://ProseFlow.UI/Assets/Icons/default.svg";

    /// <summary>
    /// If true, the result will be displayed in a dedicated window by default.
    /// If false, it will attempt an in-place replacement.
    /// </summary>
    public bool OpenInWindow { get; set; }

    /// <summary>
    /// If true, the prompt will be augmented to ask the AI to explain its changes.
    /// </summary>
    public bool ExplainChanges { get; set; }

    /// <summary>
    /// A list of application process names where this action should be prioritized or exclusively shown.
    /// An empty list means the action is globally available.
    /// </summary>
    public List<string> ApplicationContext { get; set; } = [];

    /// <summary>
    /// The display order of the action in the Floating Action Menu.
    /// </summary>
    public int SortOrder { get; set; }
}