namespace Library.Domain.Common;

/// <summary>
/// Category of an expected business failure. Each category maps to a single HTTP status at the API boundary.
/// </summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unprocessable
}
