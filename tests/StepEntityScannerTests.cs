using System.IO;

using Bingosoft.Net.IfcMetadata.FastStep;

using Xunit;

namespace IfcMetadata.Tests;

public sealed class StepEntityScannerTests
{
    [Fact]
    public void Scan_IndexesKnownEntities_AndRelations()
    {
        const string step = """
        ISO-10303-21;
        DATA;
        #10=IFCPROJECT('project-guid',$, 'Project Name',$,$,$,$,$,$);
        #20=IFCPROPERTYSET('pset-guid', $, 'Pset', $, ());
        #30=IFCRELAGGREGATES('rel-guid',$,$,$,#10,(#11,#12));
        #40=IFCRELCONTAINEDINSPATIALSTRUCTURE('rel2-guid',$,$,$,(#12,#13),#11);
        #50=IFCRELDEFINESBYPROPERTIES('rel3-guid',$,$,$,(#12),#20);
        #60=IFCRELASSOCIATESMATERIAL('rel4-guid',$,$,$,(#12),#200);
        #70=IFCRELDEFINESBYTYPE('rel5-guid',$,$,$,(#12),#300);
        ENDSEC;
        END-ISO-10303-21;
        """;

        using var reader = new StringReader(step);
        var indexes = StepEntityScanner.Scan(reader);

        Assert.Equal(7, indexes.Entities.Count);

        Assert.True(indexes.Project.HasValue);
        Assert.Equal("project-guid", indexes.Project.Value.GlobalId);
        Assert.Equal("Project Name", indexes.Project.Value.Name);

        Assert.Equal("pset-guid", indexes.PropertySetGlobalIds[20]);

        var aggregates = Assert.Single(indexes.DecompositionRelations);
        Assert.Equal(10, aggregates.RelatingId);
        Assert.Equal(new[] { 11, 12 }, aggregates.RelatedIds);

        var containment = Assert.Single(indexes.ContainmentRelations);
        Assert.Equal(11, containment.RelatingId);
        Assert.Equal(new[] { 12, 13 }, containment.RelatedIds);

        var definesByProperties = Assert.Single(indexes.DefinesByPropertiesRelations);
        Assert.Equal(20, definesByProperties.RelatingId);
        Assert.Equal(new[] { 12 }, definesByProperties.RelatedIds);

        var associatesMaterial = Assert.Single(indexes.AssociatesMaterialRelations);
        Assert.Equal(200, associatesMaterial.RelatingId);
        Assert.Equal(new[] { 12 }, associatesMaterial.RelatedIds);

        var definesByType = Assert.Single(indexes.DefinesByTypeRelations);
        Assert.Equal(300, definesByType.RelatingId);
        Assert.Equal(new[] { 12 }, definesByType.RelatedIds);
    }

    [Fact]
    public void Scan_HandlesEscapedQuotes_InStepStrings()
    {
        const string step = "#10=IFCPROJECT('guid',$,'Project ''A''',$,$,$,$,$,$);";

        using var reader = new StringReader(step);
        var indexes = StepEntityScanner.Scan(reader);

        Assert.True(indexes.Project.HasValue);
        Assert.Equal("Project 'A'", indexes.Project.Value.Name);
    }
}
