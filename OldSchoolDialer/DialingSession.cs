namespace OldSchoolDialer;

// The automatic lock only advances after the operator saves a result successfully.
public sealed class DialingSession
{
    public bool Running { get; private set; }
    public bool AutoAdvance { get; set; }
    public Guid? PendingAttemptId { get; private set; }
    public bool AwaitingResult => PendingAttemptId.HasValue;
    public void Start() => Running = true;
    public void Pause() => Running = false;
    public void CallStarted() => PendingAttemptId ??= Guid.NewGuid();
    public void CancelAttempt() { PendingAttemptId = null; Running = false; }
    public bool ResultSaved(bool? callNext = null)
    {
        if (!AwaitingResult) return false;
        PendingAttemptId = null;
        return Running && (callNext ?? AutoAdvance);
    }
    public void Reset() { Running = false; AutoAdvance = false; PendingAttemptId = null; }
}
