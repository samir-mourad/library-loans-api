using Library.Domain.Common;

namespace Library.Domain.Books;

/// <summary>
/// Fields actually modified by <see cref="Book.Update"/>, keyed by their public (camelCase) name.
/// </summary>
public sealed class BookChanges
{
    private readonly Dictionary<string, FieldChange> _fields = [];

    public IReadOnlyDictionary<string, FieldChange> Fields => _fields;

    public bool HasChanges => _fields.Count > 0;

    internal void Add(string field, object? from, object? to) => _fields[field] = new FieldChange(from, to);
}
