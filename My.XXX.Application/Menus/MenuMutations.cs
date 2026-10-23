using My.XXX.Application.Abstractions.Results;
using My.XXX.Application.AccessControl.Ports;
using My.XXX.Application.Menus.Mapping;
using My.XXX.Application.Menus.Models;
using My.XXX.Domain.Menus.Models;
using My.XXX.Domain.Menus.Policies;
using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace My.XXX.Application.Menus;

/// <summary>编排菜单存储与领域策略。所有依赖状态的决策都在适配器提供的加锁事务内运行。</summary>
internal sealed class MenuMutations(TimeProvider clock)
{
    public async Task<Result> Add(IMenuWriteSession session, MenuCreation input, string userId, CancellationToken cancellationToken = default)
    {
        var validation = ValidateAdd(input);
        if (validation.IsFailed) return validation;
        if (!MenuHierarchy.CanPlace((await session.LoadMenuStructure(cancellationToken)), 0, input.ParentId, input.IsAction!.Value))
            return Result.Fail(MenuErrors.InvalidParent().ToError());
        var now = clock.GetUtcNow().UtcDateTime;
        var menu = new MenuState
        {
            DisplayName = input.DisplayName.Trim(),
            Description = input.Description,
            Icon = input.Icon,
            Destination = new MenuDestination(input.Url, input.Component, input.ControllerName?.Trim(),
                input.ActionName?.Trim(), input.LinkTarget),
            Number = input.Number,
            ParentId = input.ParentId,
            IsDisplay = input.IsDisplay,
            IsAction = input.IsAction.Value,
            CreatedBy = userId,
            UpdatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        return Result.OkIf((await session.Insert(menu, cancellationToken)) == 1, MenuErrors.WriteFailed().ToError());
    }

    public async Task<Result> Update(IMenuWriteSession session, MenuModification input, string userId, CancellationToken cancellationToken = default)
    {
        var validation = ValidateUpdate(input);
        if (validation.IsFailed) return validation;
        var menus = await session.LoadMenuStructure(cancellationToken);
        var current = await session.LoadMenu(input.Id, cancellationToken);
        var transition = MenuTransitions.Update(current, menus, new MenuUpdate
        {
            ClearFields = MenuFieldMapping.ToDomain(input.ClearFields),
            Id = input.Id,
            DisplayName = input.DisplayName,
            Description = input.Description,
            Icon = input.Icon,
            Address = input.Url,
            View = input.Component,
            Resource = input.ControllerName,
            Operation = input.ActionName,
            OpenMode = input.LinkTarget,
            Number = input.Number,
            ParentId = input.ParentId,
            IsDisplay = input.IsDisplay,
            IsAction = input.IsAction,
        }, userId, clock.GetUtcNow().UtcDateTime).ToResult();
        if (transition.IsFailed) return Result.Fail(transition.Errors);
        var menu = transition.Value;
        return Result.OkIf((await session.Update(menu, cancellationToken)) == 1, MenuErrors.WriteFailed().ToError());
    }

    public async Task<Result> Remove(IMenuWriteSession session, List<int> ids, string userId, CancellationToken cancellationToken = default)
    {
        var validation = ValidateRemove(ids);
        if (validation.IsFailed) return validation;
        var selected = ids.Distinct().ToList();
        var menus = await session.LoadMenuStructure(cancellationToken);
        var decision = MenuRemoval.Validate(menus, selected).ToResult();
        if (decision.IsFailed) return decision;
        return Result.OkIf((await session.Remove(selected, userId, clock.GetUtcNow().UtcDateTime, cancellationToken)) == selected.Count, MenuErrors.WriteFailed().ToError());
    }

    public async Task<Result> Move(IMenuWriteSession session, MenuReorderCommand input, string userId, CancellationToken cancellationToken = default)
    {
        var menus = (await session.LoadMenus(cancellationToken));
        var order = MenuOrder.Build(menus, input);
        if (order == null) return Result.Fail(MenuErrors.InvalidOrder().ToError());
        var current = order.First(m => m.Id == input.CurrentId);
        if (!MenuHierarchy.CanPlace(menus, current.Id, current.ParentId, current.IsAction)) return Result.Fail(MenuErrors.InvalidParent().ToError());
        var originalById = menus.ToDictionary(menu => menu.Id);
        var timestamp = clock.GetUtcNow().UtcDateTime;
        for (var index = 0; index < order.Count; index++)
        {
            var candidate = order[index];
            var original = originalById[candidate.Id];
            // 未改变位置的节点保留原审计信息，减少全局锁内的数据库往返。
            if (original.Number == index && original.ParentId == candidate.ParentId)
            {
                continue;
            }
            var menu = candidate with { Number = index, UpdatedBy = userId, UpdatedAt = timestamp };
            if ((await session.Update(menu, cancellationToken)) != 1)
            {
                return Result.Fail(MenuErrors.WriteFailed().ToError());
            }
        }
        return Result.Ok();
    }

    public static Result ValidateAdd(MenuCreation input) => input == null ? Result.Fail(MenuErrors.InvalidInput().ToError())
        : MenuInputRules.Create(input.DisplayName, input.IsAction, input.ParentId, input.Number).ToResult();

    public static Result ValidateUpdate(MenuModification input) => input == null ? Result.Fail(MenuErrors.InvalidInput().ToError())
        : MenuInputRules.Modify(input.Id, input.ParentId, input.Number, MenuFieldMapping.ToDomain(input.ClearFields)).ToResult();

    public static Result ValidateRemove(List<int> ids) => MenuRemoval.ValidateSelection(ids).ToResult();

}
