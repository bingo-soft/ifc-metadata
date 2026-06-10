namespace Bingosoft.Net.IfcMetadata;

internal enum IfcExportEngine
{
    Xbim,
    FastStep,
}

internal static class IfcExportEngineParser
{
    internal static bool TryParse(string value, out IfcExportEngine engine)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "xbim":
                engine = IfcExportEngine.Xbim;
                return true;
            case "fast-step":
            case "faststep":
                engine = IfcExportEngine.FastStep;
                return true;
            default:
                engine = IfcExportEngine.Xbim;
                return false;
        }
    }
}
