namespace My.XXX.APIs.Configurations;

public sealed class TelemetrySettings
{
    public bool Enabled { get; set; } = true;

    public bool OtlpEnabled { get; set; }

    public string? ServiceName { get; set; }

    public double TraceSampleRatio { get; set; } = 1;
}