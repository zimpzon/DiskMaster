namespace DiskMasterLib
{
    public enum RunState
    {
        WaitingForRun,
        Running,
        PausePending,
        Paused,
        StopPending,
        Stopped,
        Completed,
    }
}
