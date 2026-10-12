namespace My.XXX.Domain.Menus.Models;

/// <summary>领域侧的菜单重排命令。表现层 DTO 在应用层映射到此类型，Domain 不依赖 Contracts。</summary>
public sealed record MenuReorderCommand(int CurrentId, int PrevId, int NextId);
