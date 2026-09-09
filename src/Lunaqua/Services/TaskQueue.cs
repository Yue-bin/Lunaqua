namespace Lunaqua.Services;

/// <summary>串行任务队列：同一时刻只跑一个安装/卸载动作（规格书 §7.5）。</summary>
public sealed class TaskQueue
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _busy;

    /// <summary>是否有任务在跑。</summary>
    public bool IsBusy => Volatile.Read(ref _busy) == 1;

    public event EventHandler? BusyChanged;

    public async Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        await _gate.WaitAsync();
        SetBusy(true);
        try
        {
            return await work();
        }
        finally
        {
            SetBusy(false);
            _gate.Release();
        }
    }

    public Task RunAsync(Func<Task> work) => RunAsync(async () =>
    {
        await work();
        return true;
    });

    private void SetBusy(bool value)
    {
        Volatile.Write(ref _busy, value ? 1 : 0);
        BusyChanged?.Invoke(this, EventArgs.Empty);
    }
}
