namespace KeyboardSwitcher;

public sealed class TextInputBuffer
{
    private const int Capacity = 128;

    private readonly KeyStroke[] _buffer =
        new KeyStroke[Capacity];

    private int _start;
    private int _count;

    public int Count => _count;

    public IntPtr SourceHkl { get; private set; }

    public IntPtr Hwnd { get; private set; }

    public bool IsEmpty =>
        _count == 0;

    public void Add(
        KeyStroke keyStroke,
        IntPtr sourceHkl,
        IntPtr hwnd)
    {
        if (_count == 0)
        {
            SourceHkl = sourceHkl;
            Hwnd = hwnd;
        }

        int index =
            (_start + _count) % Capacity;

        if (_count < Capacity)
        {
            _buffer[index] = keyStroke;
            _count++;
        }
        else
        {
            _buffer[index] = keyStroke;

            _start =
                (_start + 1) % Capacity;
        }
    }

    public void Clear()
    {
        _start = 0;
        _count = 0;

        SourceHkl = IntPtr.Zero;
        Hwnd = IntPtr.Zero;
    }

    public IReadOnlyList<KeyStroke> GetBuffer()
    {
        var result =
            new List<KeyStroke>(_count);

        for (int i = 0; i < _count; i++)
        {
            int index =
                (_start + i) % Capacity;

            result.Add(_buffer[index]);
        }

        return result;
    }
}