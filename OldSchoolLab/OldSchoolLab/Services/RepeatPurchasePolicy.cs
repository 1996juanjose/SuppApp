namespace OldSchool.Sales;

// Shared rule, copied into the API for its independent Docker build.
public sealed record PurchaseHistoryEntry(int Id, int? CompanyId, string Cellphone, DateTime CreatedAt);

public static class RepeatPurchasePolicy
{
    public const int WindowDays = 30;
    public static string NormalizePhone(string phone) => new(phone.Where(char.IsDigit).ToArray());
    public static bool CanCreateNext(DateTime lastCreatedAt, DateTime now) => now > lastCreatedAt.AddDays(WindowDays);

    public static Dictionary<int, int> GetPurchaseNumbers(IEnumerable<PurchaseHistoryEntry> history)
    {
        var numbers = new Dictionary<int, int>();
        foreach (var group in history.GroupBy(x => (x.CompanyId, Phone: NormalizePhone(x.Cellphone))))
        {
            var number = 0;
            DateTime? previous = null;
            foreach (var entry in group.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
            {
                if (!previous.HasValue || CanCreateNext(previous.Value, entry.CreatedAt)) number++;
                numbers[entry.Id] = number;
                previous = entry.CreatedAt;
            }
        }
        return numbers;
    }
}
