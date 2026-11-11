namespace PantryPal.Services;

public sealed class AutocompleteSearchGeneration
{
    private int _current;

    public int Begin() => Interlocked.Increment(ref _current);

    public bool IsCurrent(int generation) =>
        generation == Volatile.Read(ref _current);
}
