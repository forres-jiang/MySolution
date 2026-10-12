using System;
namespace My.XXX.Application.Abstractions.Models;

public sealed record PageWindow(int PageIndex, int Size)
{
    public int Offset => PageIndex * Size;
    public static PageWindow FromRequest(int page, int size)
    {
        var limit = Math.Clamp(size, 1, 100);
        return new PageWindow(Math.Clamp(page <= 0 ? 0 : page - 1, 0, int.MaxValue / limit), limit);
    }
}
