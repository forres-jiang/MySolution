namespace My.XXX.Domain.Menus.Models;

public interface IMenuStructure
{
    int Id { get; }
    int ParentId { get; }
    bool IsAction { get; }
}

/// <summary>Only the state required to validate hierarchy and removal.</summary>
public sealed record MenuStructure(int Id, int ParentId, bool IsAction) : IMenuStructure;
