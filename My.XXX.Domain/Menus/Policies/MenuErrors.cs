using My.XXX.Domain.Abstractions;

namespace My.XXX.Domain.Menus.Policies;

public static class MenuErrors
{
    public static BusinessError InvalidInput() => Error("Menu.InvalidInput", "Invalid menu input.");
    public static BusinessError NotFound() => Error("Menu.NotFound", "Menu not found.", BusinessErrorKind.NotFound);
    public static BusinessError InvalidParent() => Error("Menu.InvalidParent", "Invalid menu parent.");
    public static BusinessError HasChildren() => Error("Menu.HasChildren", "Remove child menus before deleting their parent.", BusinessErrorKind.Conflict);
    public static BusinessError InvalidSelection() => Error("Menu.InvalidSelection", "Invalid role or menu selection.");
    public static BusinessError InvalidOrder() => Error("Menu.InvalidOrder", "Invalid menu order.");
    public static BusinessError WriteFailed() => Error("Menu.WriteFailed", "Save failed.", BusinessErrorKind.Conflict);
    private static BusinessError Error(string code, string message, BusinessErrorKind kind = BusinessErrorKind.Validation) => new(message, code: code, kind: kind);
}
