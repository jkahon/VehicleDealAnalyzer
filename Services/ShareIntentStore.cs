namespace VehicleDealAnalyzer.Services;

public class ShareIntentStore
{
    private readonly Queue<string> _pendingItems = new();
    private readonly object _syncRoot = new();

    public event EventHandler? PendingItemReceived;

    public void Enqueue(string sharedText)
    {
        if (string.IsNullOrWhiteSpace(sharedText))
        {
            return;
        }

        lock (_syncRoot)
        {
            _pendingItems.Enqueue(sharedText.Trim());
        }

        PendingItemReceived?.Invoke(this, EventArgs.Empty);
    }

    public bool TryDequeue(out string? sharedText)
    {
        lock (_syncRoot)
        {
            if (_pendingItems.Count == 0)
            {
                sharedText = null;
                return false;
            }

            sharedText = _pendingItems.Dequeue();
            return true;
        }
    }
}
