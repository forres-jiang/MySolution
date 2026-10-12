using My.XXX.Domain.Menus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
namespace My.XXX.Domain.Menus.Policies;

public static class MenuUpdateFields
{
    public static bool Valid(IEnumerable<MenuOptionalField>? fields) => fields == null || fields.All(Enum.IsDefined);
}
