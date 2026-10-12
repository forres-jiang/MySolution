using My.XXX.Domain.Menus.Models;
using System.Collections.Generic;
namespace My.XXX.Application.Menus.Models;

public sealed class MenuNode(MenuState menu)
{
    public MenuState Menu { get; } = menu;
    public bool Checked { get; set; }
    public List<MenuNode> Actions { get; set; }
    public List<MenuNode> Children { get; set; }
}
