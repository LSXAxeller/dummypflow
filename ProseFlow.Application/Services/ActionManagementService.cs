using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProseFlow.Application.DTOs;
using ProseFlow.Core.Interfaces;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.Application.Services;

/// <summary>
/// Manages CRUD operations and business logic for Actions.
/// </summary>
public class ActionManagementService(IUnitOfWork unitOfWork, ILogger<ActionManagementService> logger)
{
    public async Task<List<Action>> GetActionsAsync()
    {
        return await unitOfWork.Actions.GetAllOrderedAsync();
    }

    public async Task UpdateActionAsync(Action action)
    {
        var trackedAction = await unitOfWork.Actions.GetByIdAsync(action.Id);

        if (trackedAction is null)
        {
            logger.LogWarning("Action with ID {ActionId} not found.", action.Id);
            throw new InvalidOperationException($"Action with ID {action.Id} not found.");
        }

        trackedAction.Name = action.Name;
        trackedAction.Prefix = action.Prefix;
        trackedAction.Instruction = action.Instruction;
        trackedAction.Icon = action.Icon;
        trackedAction.OpenInWindow = action.OpenInWindow;
        trackedAction.ExplainChanges = action.ExplainChanges;
        trackedAction.ApplicationContext = action.ApplicationContext;
        
        unitOfWork.Actions.Update(trackedAction);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task CreateActionAsync(Action action)
    {
        var maxSortOrder = await unitOfWork.Actions.GetMaxSortOrderAsync();
        action.SortOrder = maxSortOrder + 1;

        await unitOfWork.Actions.AddAsync(action);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteActionAsync(int actionId)
    {
        var action = await unitOfWork.Actions.GetByIdAsync(actionId);
        if (action is not null)
        {
            unitOfWork.Actions.Delete(action);
            await unitOfWork.SaveChangesAsync();
        }
    }

    public async Task UpdateActionOrderAsync(List<Action> orderedActions)
    {
        await unitOfWork.Actions.UpdateOrderAsync(orderedActions);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task ExportActionsToJsonAsync(string filePath)
    {
        var actions = await GetActionsAsync();
        var actionDtos = actions.ToDictionary(
            a => a.Name,
            a => new ActionDto
            {
                Prefix = a.Prefix,
                Instruction = a.Instruction,
                Icon = a.Icon,
                OpenInWindow = a.OpenInWindow,
                ExplainChanges = a.ExplainChanges,
                ApplicationContext = a.ApplicationContext
            });

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(actionDtos, jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task ImportActionsFromJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var actionDtos = JsonSerializer.Deserialize<Dictionary<string, ActionDto>>(json);

        if (actionDtos is null) return;

        // Check for duplicates
        var existingActionNames = await unitOfWork.Actions.GetAllNamesAsync();
        var maxSortOrder = await unitOfWork.Actions.GetMaxSortOrderAsync();

        foreach (var (name, dto) in actionDtos)
        {
            if (existingActionNames.Contains(name)) continue; // Skip duplicates

            var newAction = new Action
            {
                Name = name,
                Prefix = dto.Prefix,
                Instruction = dto.Instruction,
                Icon = dto.Icon,
                OpenInWindow = dto.OpenInWindow,
                ExplainChanges = dto.ExplainChanges,
                ApplicationContext = dto.ApplicationContext,
                SortOrder = ++maxSortOrder
            };

            await unitOfWork.Actions.AddAsync(newAction);
        }

        await unitOfWork.SaveChangesAsync();
    }
}