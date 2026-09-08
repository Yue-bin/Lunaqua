using Lunaqua.Models;

namespace Lunaqua.Services;

/// <summary>玩家设置的内存态 + 落盘。</summary>
public interface ISettingsService
{
    /// <summary>当前设置（Load 之后始终非 null）。</summary>
    AppSettings Current { get; }

    /// <summary>设置被修改并落盘后触发。</summary>
    event EventHandler? Changed;

    /// <summary>从磁盘读取；文件不存在或损坏时使用默认值。</summary>
    void Load();

    /// <summary>原子写回磁盘（先写临时文件再替换）。</summary>
    void Save();

    /// <summary>改一处 + 落盘 + 通知。</summary>
    void Update(Action<AppSettings> mutate);
}
