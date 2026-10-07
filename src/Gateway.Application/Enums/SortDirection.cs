namespace Gateway.Application.Enums;

/// <summary>
/// The direction a paged-readings query sorts in.
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// Smallest/earliest first.
    /// </summary>
    Ascending = 0,

    /// <summary>
    /// Largest/latest first.
    /// </summary>
    Descending = 1,
}
