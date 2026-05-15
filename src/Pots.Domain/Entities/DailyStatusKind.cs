namespace Pots.Domain.Entities;

// Names match the SQL literals stored via HasConversion<string>(). Renaming
// breaks RLS-adjacent queries and any historical rows. Pinned by tests.
public enum DailyStatusKind
{
    Green = 1,
    Orange = 2,
    Red = 3,
}
