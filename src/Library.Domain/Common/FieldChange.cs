namespace Library.Domain.Common;

/// <summary>
/// Previous and new value of a single field, used to describe changes in audit events.
/// </summary>
public sealed record FieldChange(object? From, object? To);
