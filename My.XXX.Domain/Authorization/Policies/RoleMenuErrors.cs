using My.XXX.Domain.Abstractions;

namespace My.XXX.Domain.Authorization.Policies;

public static class RoleMenuErrors
{
    // Preserve legacy result codes while keeping authorization rules independent of menu policies.
    public static BusinessError InvalidSelection() => new("Invalid role or menu selection.", code: "Menu.InvalidSelection");
    public static BusinessError WriteFailed() => new("Save failed.", code: "Menu.WriteFailed", kind: BusinessErrorKind.Conflict);
}
