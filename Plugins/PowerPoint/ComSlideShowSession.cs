using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ControlHub.Plugins.PowerPoint;

[SupportedOSPlatform("windows")]
public sealed class ComSlideShowSession : ISlideShowSession
{
    private readonly BlockingCollection<Action> queue = new();
    private readonly Thread thread;
    private int disposed;
    public ComSlideShowSession()
    {
        thread = new Thread(() => { foreach (var action in queue.GetConsumingEnumerable()) action(); })
            { IsBackground = true, Name = "ControlHub.PowerPoint.STA" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private async Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Add(() =>
        {
            if (cancellationToken.IsCancellationRequested) { completion.TrySetCanceled(cancellationToken); return; }
            try { completion.TrySetResult(action()); }
            catch (Exception ex) { completion.TrySetException(ex); }
        }, cancellationToken);
        // COM cannot be forcibly cancelled; the client must reconcile an unknown result.
        // COM kan ikke tvangsafbrydes; klienten skal kontrollere status efter timeout.
        return await completion.Task.WaitAsync(cancellationToken);
    }

    public Task<SlideShowSnapshot> ReadAsync(CancellationToken cancellationToken)
        => InvokeAsync(() => WithView((view, count) => new SlideShowSnapshot((int)view.CurrentShowPosition, count, (int)view.State)), cancellationToken);

    public async Task ApplyAsync(string action, int? slide, CancellationToken cancellationToken)
    {
        await InvokeAsync(() => WithView((view, count) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (action)
            {
                case "next": view.Next(); break;
                case "previous": view.Previous(); break;
                case "goto":
                    if (slide is null || slide < 1 || slide > count) throw new ArgumentOutOfRangeException(nameof(slide));
                    view.GotoSlide(slide.Value, -1); break;
                case "black": view.State = 3; break;
                case "resume": view.State = 1; break;
                default: throw new ArgumentException("Unknown action / Ukendt handling.", nameof(action));
            }
            return true;
        }), cancellationToken);
    }

    private static T WithView<T>(Func<dynamic, int, T> action)
    {
        var objects = new List<object>();
        object Track(object value) { objects.Add(value); return value; }
        try
        {
            var clsid = new Guid("91493441-5A91-11CF-8700-00AA0060263B");
            GetActiveObject(ref clsid, IntPtr.Zero, out var active);
            dynamic app = Track(active);
            dynamic windows = Track(app.SlideShowWindows);
            if ((int)windows.Count != 1) throw new SlideShowUnavailableException();
            dynamic window = Track(windows.Item(1));
            dynamic presentation = Track(window.Presentation);
            dynamic slides = Track(presentation.Slides);
            dynamic view = Track(window.View);
            return action(view, (int)slides.Count);
        }
        catch (COMException) { throw new SlideShowUnavailableException(); }
        finally
        {
            for (var i = objects.Count - 1; i >= 0; i--)
                if (Marshal.IsComObject(objects[i])) Marshal.ReleaseComObject(objects[i]);
        }
    }

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid clsid, IntPtr reserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object result);

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            queue.CompleteAdding();
            thread.Join(TimeSpan.FromSeconds(2));
        }
        return ValueTask.CompletedTask;
    }
}
