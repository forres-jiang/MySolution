using System.Collections.Generic;

namespace My.XXX.Application.Abstractions;

public class Paged<T>
{
    public int Total { get; protected set; }

    public List<T> List { get; protected set; }

    public Paged(List<T> data, int total)
    {
        Total = total;
        List = data;
    }

    public static Paged<T> Create(List<T> data, int Total)
    {
        return new Paged<T>(data, Total);
    }
}
