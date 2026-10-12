using System;

namespace My.XXX.Domain.Menus.Models;

/// <summary>供纯业务策略使用的不可变状态快照；写入前由策略验证，绝不由 HTTP 端点直接返回。</summary>
public sealed record MenuState : IMenuStructure
{
    public int Id { get; init; }
    public string DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public int Number { get; init; }
    public MenuDestination Destination { get; init; } = new();
    public int ParentId { get; init; }
    public bool IsDisplay { get; init; }
    public bool IsAction { get; init; }
    public bool IsDeleted { get; init; }
    public string CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public string UpdatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public MenuState Copy() => (MenuState)MemberwiseClone();
}
