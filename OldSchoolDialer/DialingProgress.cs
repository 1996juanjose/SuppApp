namespace OldSchoolDialer;

public sealed class DialingProgress
{
    public int Total { get; private set; }
    public int Saved { get; private set; }
    public int Skipped { get; private set; }
    public int Remaining => Math.Max(0, Total - Saved - Skipped);
    public double Fraction => Total == 0 ? 0 : Math.Clamp((double)(Saved + Skipped) / Total, 0, 1);
    public void Reset() { Total = Saved = Skipped = 0; }
    public void SetTotal(int total) => Total = Math.Max(0, total);
    public void AddLoaded(int count) => Total += count;
    public void RecordSaved() => Saved++;
    public void RecordSkipped() => Skipped++;
    public string Summary => $"Llamados: {Saved}  ·  Faltan: {Remaining}\nTotal: {Total}  ·  Omitidos: {Skipped}";
}
