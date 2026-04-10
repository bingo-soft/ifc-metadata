using System;

namespace Bingosoft.Net.IfcMetadata
{
    internal static class IfcSchemaRouter
    {
        internal static bool IsIfc2x3(string schemaVersion)
        {
            return !string.IsNullOrWhiteSpace(schemaVersion)
                   && schemaVersion.StartsWith("IFC2X3", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsIfc4(string schemaVersion)
        {
            if (string.IsNullOrWhiteSpace(schemaVersion))
            {
                return false;
            }

            return schemaVersion.StartsWith("IFC4", StringComparison.OrdinalIgnoreCase)
                   && !schemaVersion.StartsWith("IFC4X3", StringComparison.OrdinalIgnoreCase);
        }
    }
}
