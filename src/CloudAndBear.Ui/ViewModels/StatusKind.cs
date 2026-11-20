namespace CloudAndBear.Ui.ViewModels;

/// <summary>Kako se stanje boji: zeleno radi, sivo ne radi ili je završeno, žuto čeka, crveno puca.</summary>
public enum StatusKind
{
    Good,
    Off,
    Warn,
    Bad,
}

public static class StatusKinds
{
    public static StatusKind ForContainer(string state) => state switch
    {
        "running" => StatusKind.Good,
        "exited" or "created" => StatusKind.Off,
        "dead" => StatusKind.Bad,
        _ => StatusKind.Warn,   // paused, restarting, removing
    };

    public static StatusKind ForKube(string status) => status switch
    {
        "Running" or "Ready" => StatusKind.Good,
        "Completed" or "Succeeded" => StatusKind.Off,
        "Pending" or "ContainerCreating" or "PodInitializing" or "Terminating" or "Unknown" => StatusKind.Warn,
        _ when status.StartsWith("Init:") => StatusKind.Warn,
        _ => StatusKind.Bad,   // CrashLoopBackOff, ImagePullBackOff, Error, Failed, NotReady...
    };
}

/// <summary>Red sa bojom stanja; pogled bira stil preko IsGood/IsWarn/IsBad (siva je podrazumevana).</summary>
public abstract record StatusRow
{
    public abstract StatusKind Kind { get; }

    public bool IsGood => Kind == StatusKind.Good;
    public bool IsWarn => Kind == StatusKind.Warn;
    public bool IsBad => Kind == StatusKind.Bad;
}
