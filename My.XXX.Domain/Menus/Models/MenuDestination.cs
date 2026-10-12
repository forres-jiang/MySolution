namespace My.XXX.Domain.Menus.Models;

/// <summary>Opaque navigation identifiers. Their interpretation belongs to the consuming adapter.</summary>
public sealed record MenuDestination(string? Address = null, string? View = null,
    string? Resource = null, string? Operation = null, string? OpenMode = null);

public enum MenuOptionalField { Description, Icon, Address, View, Resource, Operation, OpenMode }
