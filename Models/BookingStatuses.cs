namespace EventEase.Models
{
    public static class BookingStatuses
    {
        public const string Default = Booked;

        public const string Booked = "Booked";
        public const string Reserved = "Reserved";
        public const string Cancelled = "Cancelled";
        public const string Completed = "Completed";

        // Legacy rows may still use this value from earlier versions of the app.
        public const string Confirmed = "Confirmed";

        public static readonly string[] SelectOptions =
        {
            Booked,
            Reserved,
            Cancelled,
            Completed
        };

        private static readonly string[] AllowedValues =
        {
            Booked,
            Reserved,
            Cancelled,
            Completed,
            Confirmed
        };

        public static bool TryNormalize(string? value, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var match = AllowedValues.FirstOrDefault(s =>
                string.Equals(s, value.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                return false;
            }

            normalized = match;
            return true;
        }

        public static IReadOnlyList<string> GetStatusesMatchingFilter(string filterStatus)
        {
            if (!TryNormalize(filterStatus, out var normalized))
            {
                return Array.Empty<string>();
            }

            if (normalized == Booked)
            {
                return new[] { Booked, Confirmed, "booked" };
            }

            return new[] { normalized };
        }

        public static string GetBadgeClass(string? status)
        {
            if (TryNormalize(status, out var normalized))
            {
                status = normalized;
            }

            return status switch
            {
                Booked or Confirmed => "bg-success",
                Reserved => "bg-warning text-dark",
                Completed => "bg-info text-dark",
                Cancelled => "bg-danger",
                _ => "bg-secondary"
            };
        }
    }
}
