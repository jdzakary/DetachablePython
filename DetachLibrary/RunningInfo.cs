using Newtonsoft.Json;

namespace DetachLibrary;

public struct RunningInfo
{
    public RunningInfo(uint id, DateTime startTime, CancellationTokenSource source)
    {
        Id = id;
        StartTime = startTime;
        StopTime = null;
        Source = source;
    }
    public uint Id { get; }
    public DateTime StartTime { get; }
    public DateTime? StopTime { get; set; }
    
    [JsonIgnore]
    public CancellationTokenSource Source { get; }
}