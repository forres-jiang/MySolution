#nullable enable
using My.XXX.Application.Menus.Models;
using My.XXX.Domain.Menus.Models;
using Riok.Mapperly.Abstractions;
using System.Collections.Generic;
using System.Linq;
namespace My.XXX.Application.Menus.Mapping;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target,
    ThrowOnMappingNullMismatch = false, ThrowOnPropertyMappingNullMismatch = false)]
public partial class ApplicationMapper
{
    public List<MenuView> ToTreeViews(IEnumerable<MenuNode> nodes) => nodes.Select(node =>
    {
        var view = ToMenuView(node.Menu)!;
        view.Checked = node.Checked;
        view.Actions = node.Actions == null ? null : ToTreeViews(node.Actions);
        view.Children = node.Children == null ? null : ToTreeViews(node.Children);
        return view;
    }).ToList();

    [MapProperty("Destination.Address", nameof(MenuView.Url))]
    [MapProperty("Destination.View", nameof(MenuView.Component))]
    [MapProperty("Destination.Resource", nameof(MenuView.ControllerName))]
    [MapProperty("Destination.Operation", nameof(MenuView.ActionName))]
    [MapProperty("Destination.OpenMode", nameof(MenuView.LinkTarget))]
    [MapperIgnoreTarget(nameof(MenuView.Checked))]
    [MapperIgnoreTarget(nameof(MenuView.Actions))]
    [MapperIgnoreTarget(nameof(MenuView.Children))]
    public partial MenuView? ToMenuView(MenuState? source);
    public partial List<MenuView> ToMenuViews(IEnumerable<MenuState>? source);
}
