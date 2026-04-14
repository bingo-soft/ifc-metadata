using System;
using System.IO;

using Bingosoft.Net.IfcMetadata.FastStep;

using Xunit;

namespace IfcMetadata.Tests;

public sealed class StepHeaderReaderTests
{
    [Fact]
    public void Read_NormalizesIfc2x2Schema_ToIfc2x3()
    {
        var ifcPath = Path.Combine(Path.GetTempPath(), $"ifc-header-{Guid.NewGuid():N}.ifc");

        const string ifc = """
        ISO-10303-21;
        HEADER;
        FILE_NAME('model.ifc','2024-01-01T00:00:00',('author'),('org'),'app','system','auth');
        FILE_SCHEMA(('IFC2X2_FINAL'));
        ENDSEC;
        DATA;
        #10=IFCPROJECT('project-guid',$,'Project Name',$,$,$,$,$,$);
        ENDSEC;
        END-ISO-10303-21;
        """;

        try
        {
            File.WriteAllText(ifcPath, ifc);
            var header = StepHeaderReader.Read(new FileInfo(ifcPath));

            Assert.Equal("IFC2X3", header.Schema);
        }
        finally
        {
            if (File.Exists(ifcPath))
            {
                File.Delete(ifcPath);
            }
        }
    }
}
