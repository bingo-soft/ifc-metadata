using System.Text.Json;

using Bingosoft.Net.IfcMetadata;

using Xunit;

namespace IfcMetadata.Tests;

public sealed class FastStepJsonEmitterTests
{
    [Fact]
    public void FastStepExporter_WritesContractShapedJson_FromStepIndexes()
    {
        var ifcPath = Path.Combine(Path.GetTempPath(), $"ifc-fast-step-{Guid.NewGuid():N}.ifc");
        var jsonPath = Path.Combine(Path.GetTempPath(), $"ifc-fast-step-{Guid.NewGuid():N}.json");

        const string ifc = """
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
        FILE_NAME('model.ifc','2024-01-01T00:00:00',('author1','author2'),('org'),'app','system','auth');
        FILE_SCHEMA(('IFC4'));
        ENDSEC;
        DATA;
        #10=IFCPROJECT('project-guid',$,'Project Name',$,$,$,$,$,$);
        #11=IFCSITE('site-guid',$,'Site Name',$,$,$,$,$,$,$,$,$,$,$);
        #20=IFCRELAGGREGATES('rel-1',$,$,$,#10,(#11));
        #30=IFCPROPERTYSET('pset-guid',$,'Pset',$,());
        #31=IFCRELDEFINESBYPROPERTIES('rel-2',$,$,$,(#11),#30);
        #40=IFCMATERIAL('Concrete',$,$);
        #41=IFCRELASSOCIATESMATERIAL('rel-3',$,$,$,(#11),#40);
        #50=IFCTYPEOBJECT('type-guid',$,'Type Name',$,$,$,$,$);
        #51=IFCRELDEFINESBYTYPE('rel-4',$,$,$,(#11),#50);
        ENDSEC;
        END-ISO-10303-21;
        """;

        try
        {
            File.WriteAllText(ifcPath, ifc);
            var report = FastStepJsonExporter.Export(new FileInfo(ifcPath), new FileInfo(jsonPath), preserveOrder: true, 64 * 1024, writeThrough: false, progressReporter: null);

            Assert.Equal("IFC4", report.SchemaVersion);
            Assert.Equal(2, report.MetaObjectCount);

            using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            var root = document.RootElement;

            Assert.Equal("Project Name", root.GetProperty("id").GetString());
            Assert.Equal("project-guid", root.GetProperty("projectId").GetString());
            Assert.Equal("author1;author2", root.GetProperty("author").GetString());
            Assert.Equal("2024-01-01T00:00:00", root.GetProperty("createdAt").GetString());
            Assert.Equal("IFC4", root.GetProperty("schema").GetString());
            Assert.Equal("system", root.GetProperty("creatingApplication").GetString());

            var metaObjects = root.GetProperty("metaObjects");
            var project = metaObjects.GetProperty("project-guid");
            var site = metaObjects.GetProperty("site-guid");

            Assert.Equal("IfcProject", project.GetProperty("type").GetString());
            Assert.Equal(JsonValueKind.Null, project.GetProperty("properties").ValueKind);

            Assert.Equal("project-guid", site.GetProperty("parent").GetString());
            var psets = site.GetProperty("properties");
            Assert.Equal(1, psets.GetArrayLength());
            Assert.Equal("pset-guid", psets[0].GetString());
            Assert.Equal("IfcMaterial_40", site.GetProperty("material_id").GetString());
            Assert.Equal("type-guid", site.GetProperty("type_id").GetString());
        }
        finally
        {
            if (File.Exists(ifcPath))
            {
                File.Delete(ifcPath);
            }

            if (File.Exists(jsonPath))
            {
                File.Delete(jsonPath);
            }
        }
    }
}
