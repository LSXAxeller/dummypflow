using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProseFlow.Application.DTOs;
using ProseFlow.Core.Interfaces;
using ProseFlow.Core.Models;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.Application.Services;

/// <summary>
/// Manages CRUD operations and business logic for Actions and ActionGroups.
/// </summary>
public class ActionManagementService(IServiceScopeFactory scopeFactory, ILogger<ActionManagementService> logger)
{
    #region ActionGroup Management

    public Task<List<ActionGroup>> GetActionGroupsWithActionsAsync()
    {
        return ExecuteQueryAsync(unitOfWork => unitOfWork.ActionGroups.GetAllOrderedWithActionsAsync());
    }

    public Task<List<ActionGroup>> GetActionGroupsAsync()
    {
        return ExecuteQueryAsync(unitOfWork => unitOfWork.ActionGroups.GetAllOrderedAsync());
    }

    public Task CreateActionGroupAsync(ActionGroup group)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var maxSortOrder = await unitOfWork.ActionGroups.GetMaxSortOrderAsync();
            group.SortOrder = maxSortOrder + 1;
            await unitOfWork.ActionGroups.AddAsync(group);
        });
    }

    public Task UpdateActionGroupAsync(ActionGroup group)
    {
        return ExecuteCommandAsync(unitOfWork =>
        {
            unitOfWork.ActionGroups.Update(group);
            return Task.CompletedTask;
        });
    }

    public Task DeleteActionGroupAsync(int groupId)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            // The default group (ID=1) cannot be deleted.
            if (groupId == 1)
            {
                logger.LogWarning("Attempted to delete the default 'General' group.");
                return;
            }

            var groupToDelete = await unitOfWork.ActionGroups.GetByIdAsync(groupId);
            if (groupToDelete is null) return;

            var defaultGroup = await unitOfWork.ActionGroups.GetDefaultGroupAsync() ??
                               throw new InvalidOperationException("Default 'General' group not found.");

            // Re-parent all actions from the deleted group to the default group.
            var actionsToMove = await unitOfWork.Actions.GetByExpressionAsync(a => a.ActionGroupId == groupId);
            foreach (var action in actionsToMove)
            {
                action.ActionGroupId = defaultGroup.Id;
                unitOfWork.Actions.Update(action);
            }

            unitOfWork.ActionGroups.Delete(groupToDelete);
        });
    }

    public Task UpdateActionGroupOrderAsync(List<ActionGroup> orderedGroups)
    {
        return ExecuteCommandAsync(unitOfWork => unitOfWork.ActionGroups.UpdateOrderAsync(orderedGroups));
    }

    #endregion

    #region Action Management

    public Task<List<Action>> GetActionsAsync()
    {
        return ExecuteQueryAsync(unitOfWork => unitOfWork.Actions.GetAllOrderedAsync());
    }

    public Task UpdateActionAsync(Action action)
    {
        return ExecuteCommandAsync(unitOfWork =>
        {
            unitOfWork.Actions.Update(action);
            return Task.CompletedTask;
        });
    }

    public Task CreateActionAsync(Action action)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            // If no group is assigned, add it to the default group.
            if (action.ActionGroupId == 0)
            {
                var defaultGroup = await unitOfWork.ActionGroups.GetDefaultGroupAsync() ??
                                   throw new InvalidOperationException("Default 'General' group not found.");
                action.ActionGroupId = defaultGroup.Id;
            }

            var maxSortOrder = await unitOfWork.Actions.GetMaxSortOrderAsync();
            action.SortOrder = maxSortOrder + 1;

            await unitOfWork.Actions.AddAsync(action);
        });
    }

    public Task DeleteActionAsync(int actionId)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var action = await unitOfWork.Actions.GetByIdAsync(actionId);
            if (action is not null) unitOfWork.Actions.Delete(action);
        });
    }

    public Task UpdateActionOrderAsync(int actionId, int targetGroupId, int newIndex)
    {
        return ExecuteCommandAsync(async unitOfWork =>
        {
            var actionToMove = await unitOfWork.Actions.GetByIdAsync(actionId) ??
                               throw new InvalidOperationException("Action not found.");
            var originalGroupId = actionToMove.ActionGroupId;

            // Get all actions from the target group
            var targetGroupActions = (await unitOfWork.Actions.GetByExpressionAsync(a => a.ActionGroupId == targetGroupId))
                .OrderBy(a => a.SortOrder).ToList();

            // If moving within the same group, remove the item first to get the correct index
            if (originalGroupId == targetGroupId) targetGroupActions.RemoveAll(a => a.Id == actionId);

            // Insert at the new position and update the ActionGroupId
            actionToMove.ActionGroupId = targetGroupId;
            targetGroupActions.Insert(Math.Clamp(newIndex, 0, targetGroupActions.Count), actionToMove);

            // Re-number the sort order for the entire target group
            for (var i = 0; i < targetGroupActions.Count; i++)
            {
                targetGroupActions[i].SortOrder = i;
                unitOfWork.Actions.Update(targetGroupActions[i]);
            }

            // If moved from a different group, re-number the source group as well
            if (originalGroupId != targetGroupId)
            {
                var sourceGroupActions =
                    (await unitOfWork.Actions.GetByExpressionAsync(a => a.ActionGroupId == originalGroupId))
                    .OrderBy(a => a.SortOrder).ToList();
                for (var i = 0; i < sourceGroupActions.Count; i++)
                {
                    sourceGroupActions[i].SortOrder = i;
                    unitOfWork.Actions.Update(sourceGroupActions[i]);
                }
            }
        });
    }

    #endregion

    #region Import/Export

    public async Task ExportActionsToJsonAsync(string filePath)
    {
        var groupsWithActions = await GetActionGroupsWithActionsAsync();

        var exportData = groupsWithActions.ToDictionary(
            g => g.Name,
            g => g.Actions.ToDictionary(
                a => a.Name,
                a => new ActionDto
                {
                    Prefix = a.Prefix,
                    Instruction = a.Instruction,
                    Icon = a.Icon,
                    OpenInWindow = a.OpenInWindow,
                    ExplainChanges = a.ExplainChanges,
                    ApplicationContext = a.ApplicationContext
                }));

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(exportData, jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task ImportActionsFromJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var importedData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, ActionDto>>>(json);

        if (importedData is null) return;

        // Use a single UnitOfWork for the entire import operation for performance and transactional integrity.
        using var scope = scopeFactory.CreateScope();
        await using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var existingGroups = await unitOfWork.ActionGroups.GetAllAsync();
        var allExistingActions = await unitOfWork.Actions.GetAllAsync();
        var maxActionSortOrder = await unitOfWork.Actions.GetMaxSortOrderAsync();
        var maxGroupSortOrder = await unitOfWork.ActionGroups.GetMaxSortOrderAsync();

        foreach (var (groupName, actions) in importedData)
        {
            // Find or create the group
            var group = existingGroups.FirstOrDefault(g =>
                g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
            if (group is null)
            {
                maxGroupSortOrder++;
                group = new ActionGroup { Name = groupName, SortOrder = maxGroupSortOrder };
                await unitOfWork.ActionGroups.AddAsync(group);
                existingGroups.Add(group); // Add to local list for subsequent checks within this transaction
            }

            foreach (var (actionName, dto) in actions)
            {
                // Skip if an action with this name already exists anywhere
                if (allExistingActions.Any(a => a.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase))) continue;

                maxActionSortOrder++;
                var newAction = new Action
                {
                    Name = actionName,
                    Prefix = dto.Prefix,
                    Instruction = dto.Instruction,
                    Icon = dto.Icon,
                    OpenInWindow = dto.OpenInWindow,
                    ExplainChanges = dto.ExplainChanges,
                    ApplicationContext = dto.ApplicationContext,
                    ActionGroup = group, // Use navigation property; EF handles the ID
                    SortOrder = maxActionSortOrder
                };

                await unitOfWork.Actions.AddAsync(newAction);
            }
        }
        
        // Commit all changes in a single transaction.
        await unitOfWork.SaveChangesAsync();
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Creates a UoW scope, executes a command, and saves changes.
    /// </summary>
    private async Task ExecuteCommandAsync(Func<IUnitOfWork, Task> command)
    {
        using var scope = scopeFactory.CreateScope();
        await using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await command(unitOfWork);
        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a UoW scope and executes a query.
    /// </summary>
    private async Task<T> ExecuteQueryAsync<T>(Func<IUnitOfWork, Task<T>> query)
    {
        using var scope = scopeFactory.CreateScope();
        await using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await query(unitOfWork);
    }

    #endregion
}