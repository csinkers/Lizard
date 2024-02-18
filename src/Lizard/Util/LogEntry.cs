using System.Numerics;
using ImGuiColorTextEditNet;

namespace Lizard.Util;

public record LogEntry(Severity Severity, Line Line)
{
    public Vector4 Color =>
        Severity switch
        {
            Severity.Debug => new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
            Severity.Info => new Vector4(0.3f, 1.0f, 1.0f, 1.0f),
            Severity.Warn => new Vector4(1.0f, 1.0f, 0.3f, 1.0f),
            Severity.Error => new Vector4(1.0f, 0.3f, 0.3f, 1.0f),
            _ => new Vector4(0.85f, 0.85f, 0.85f, 1.0f),
        };
}
