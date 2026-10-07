namespace KeyboardSwitcher;

public sealed class TextInputTracker
{
    public TextInputBuffer ActiveBuffer { get; } =
        new TextInputBuffer();

    public TextInputBuffer PendingBuffer { get; } =
        new TextInputBuffer();

    public int Count =>
        ActiveBuffer.Count;

    public IntPtr SourceHkl =>
        ActiveBuffer.SourceHkl;

    // ------------------------------------------------------------
    // Стан пропуску наступного слова.
    //
    // Встановлюється після явного перемикання розкладки.
    // ------------------------------------------------------------

    //private bool _ignoreNextWord;

    //public bool IgnoreNextWord =>
    //    _ignoreNextWord;

    //public void IgnoreNextWord()
    //{
    //    _ignoreNextWord = true;
    //}

    private bool _ignoreNextWord;

    public bool IsIgnoringNextWord =>
        _ignoreNextWord;

    public void BeginIgnoreNextWord()
    {
        _ignoreNextWord = true;
    }

    public void FinishIgnoredWord()
    {
        _ignoreNextWord = false;
    }

    // ------------------------------------------------------------
    // Input
    // ------------------------------------------------------------

    public bool Add(
        KeyStroke keyStroke,
        IntPtr currentHkl,
        IntPtr hwnd)
    {
        if (currentHkl == IntPtr.Zero)
            return false;

        // Буфер порожній — починаємо новий фрагмент.
        if (ActiveBuffer.IsEmpty)
        {
            ActiveBuffer.Add(
                keyStroke,
                currentHkl,
                hwnd);

            return true;
        }

        // Розкладка змінилася —
        // починаємо новий фрагмент.
        if (ActiveBuffer.SourceHkl != currentHkl)
        {
            ActiveBuffer.Clear();

            ActiveBuffer.Add(
                keyStroke,
                currentHkl,
                hwnd);

            return true;
        }

        ActiveBuffer.Add(
            keyStroke,
            currentHkl,
            hwnd);

        return true;
    }

    // ------------------------------------------------------------
    // Clear
    // ------------------------------------------------------------

    public void Clear()
    {
        ActiveBuffer.Clear();
    }

    public void ClearPending()
    {
        PendingBuffer.Clear();
    }

    public void ClearAll()
    {
        ActiveBuffer.Clear();
        PendingBuffer.Clear();

        _ignoreNextWord = false;
    }

    // ------------------------------------------------------------
    // Pending
    // ------------------------------------------------------------

    public void SaveActiveToPending()
    {
        PendingBuffer.Clear();

        foreach (KeyStroke keyStroke
                 in ActiveBuffer.GetBuffer())
        {
            PendingBuffer.Add(
                keyStroke,
                ActiveBuffer.SourceHkl,
                ActiveBuffer.Hwnd);
        }

        ActiveBuffer.Clear();
    }

    public IReadOnlyList<KeyStroke> GetBuffer()
    {
        return ActiveBuffer.GetBuffer();
    }
}