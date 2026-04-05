namespace Haze.Models;

public enum HazeClientJobState
{
    None = 0,
    /**
     * The job was canceled by its owner or an administrator.
     */
    Canceled = 1,
    /**
     * The job ran to completion successfully and the client released allocated resources.
     */
    Completed = 2,
    /**
     * The job ran to completion unsuccessfully and the client released allocated resources.
     */
    Failed = 3,
    /**
     * The job is queued and waiting for initiation; jobs in this state
     * will typically have a reason code specifying why they have not yet started.
     */
    Pending = 4,
    /**
     * The job has started, the client is actively using allocated resources.
     */
    Running = 5,
}
