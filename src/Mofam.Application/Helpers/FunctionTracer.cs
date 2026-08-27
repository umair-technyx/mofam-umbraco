using System.Diagnostics;
using Serilog;

namespace Mofam.Application.Helpers;

public sealed class FunctionTracer : IDisposable
{
    private readonly Stopwatch? _stopwatch;
    private readonly string? _label;
    private readonly bool _loginfile;
    private readonly ILogger _logger;

    public FunctionTracer(bool loginfile = false, string alias = "")
    {
        _loginfile = loginfile;
        _logger = Log.Logger;

        if (!_loginfile) return;

        string className = "UnknownClass";
        string methodName = "UnknownMethod";

        try
        {
            var stackTrace = new StackTrace();
            var frame = stackTrace.GetFrame(1);
            var method = frame?.GetMethod();

            if (method != null)
            {
                className = method.DeclaringType?.Name ?? className;
                methodName = method.Name ?? methodName;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "FunctionTracer failed to resolve stack trace.");
        }

        alias = !string.IsNullOrEmpty(alias) ? alias + " -> " : string.Empty;
        _label = $"{alias}{className} -> {methodName}";

        _stopwatch = Stopwatch.StartNew();
        _logger.Information("{Label} - Execution Start", _label);
    }

    public void Dispose()
    {
        if (!_loginfile || _stopwatch is null) return;

        _stopwatch.Stop();
        _logger.Information("{Label} - Total Execution Time: {ElapsedSeconds} sec(s)", _label, _stopwatch.Elapsed.TotalSeconds);
    }
}
