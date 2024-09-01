namespace ActivityLogger.Models;

public record Activity<TTraceId>
{
    public TTraceId? TraceId { get; set; }
    public string? ClientIp { get; set; }
    public string? EndPoint { get; set; }
    public DateTime? RequestAt { get; set; }
    public DateTime? ResponseAt { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public int? StatusCode { get; set; }
    public string? RequestMethod { get; set; }
    public bool? IsCancelled { get; set; }
}