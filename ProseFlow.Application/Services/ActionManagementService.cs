using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ProseFlow.Application.DTOs;
using ProseFlow.Infrastructure.Data;
using Action = ProseFlow.Core.Models.Action;

namespace ProseFlow.Application.Services;

public class ActionManagementService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<Action>> GetActionsAsync()
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        return await dbContext.Actions.OrderBy(a => a.SortOrder).ToListAsync();
    }

    public async Task UpdateActionAsync(Action action)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        dbContext.Actions.Update(action);
        await dbContext.SaveChangesAsync();
    }

    public async Task CreateActionAsync(Action action)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var maxSortOrder = await dbContext.Actions.AnyAsync()
            ? await dbContext.Actions.MaxAsync(a => a.SortOrder)
            : 0;
        action.SortOrder = maxSortOrder + 1;
        dbContext.Actions.Add(action);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteActionAsync(int actionId)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var action = await dbContext.Actions.FindAsync(actionId);
        if (action is not null)
        {
            dbContext.Actions.Remove(action);
            await dbContext.SaveChangesAsync();
        }
    }


    public async Task UpdateActionOrderAsync(List<Action> orderedActions)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        for (var i = 0; i < orderedActions.Count; i++)
        {
            var actionToUpdate = await dbContext.Actions.FindAsync(orderedActions[i].Id);
            if (actionToUpdate != null) 
                actionToUpdate.SortOrder = i;
        }
        await dbContext.SaveChangesAsync();
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

        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var existingActionNames = await dbContext.Actions.Select(a => a.Name).ToListAsync();
        var maxSortOrder = existingActionNames.Count != 0 ? await dbContext.Actions.MaxAsync(a => a.SortOrder) : 0;

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
            dbContext.Actions.Add(newAction);
        }
        await dbContext.SaveChangesAsync();
    }
}