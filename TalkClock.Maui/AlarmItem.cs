namespace TalkClock;
public sealed class AlarmItem
{
    public int Id { get; set; }
    public string Label { get; set; } = "Alarm";
    public string Time { get; set; } = "07:00";
    // Sunday=0 ... Saturday=6. Empty means one-shot.
    public List<int> Days { get; set; } = [];
    public bool Enabled { get; set; } = true;
    public string RepeatText => Days.Count == 0 ? "Once" : Days.Count == 7 ? "Every day" :
        string.Join(" ", new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" }.Where((_, i) => Days.Contains(i)));
}