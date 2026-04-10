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
    public void Scan_IndexesEntityOffsets_AndArgumentRanges()
    {
        const string step = "prefix #10=IFCPROJECT('guid',$,'Project Name',$,$,$,$,$,$); suffix";

        using var reader = new StringReader(step);
        var indexes = StepEntityScanner.Scan(reader);

        var range = indexes.EntityRanges[10];
        var statementStart = step.IndexOf("#10=IFCPROJECT", System.StringComparison.Ordinal);
        var statementEnd = step.IndexOf(';', statementStart) + 1;
        var argsStart = step.IndexOf('(', statementStart) + 1;
        var argsEnd = argsStart + indexes.Entities[10].RawArguments.Length;

        Assert.Equal(statementStart, range.StatementStartOffset);
        Assert.Equal(statementEnd, range.StatementEndOffset);
        Assert.Equal(argsStart, range.ArgumentsStartOffset);
        Assert.Equal(argsEnd, range.ArgumentsEndOffset);

        var argsSlice = step[range.ArgumentsStartOffset..range.ArgumentsEndOffset];
        Assert.Equal(indexes.Entities[10].RawArguments, argsSlice);
    }

    [Fact]
    public void Scan_PoolsRepeatedStrings()
    {
        const string step = """
        #10=IFCPROJECT('project-guid',$,'Shared Name',$,$,$,$,$,$);
        #11=IFCSITE('site-guid',$,'Shared Name',$,$,$,$,$,$,$,$,$,$,$);
        #12=IFCWALL('wall-1',$,'Wall A',$,$,$,$,$);
        #13=IFCWALL('wall-2',$,'Wall B',$,$,$,$,$);
        """;

        using var reader = new StringReader(step);
        var indexes = StepEntityScanner.Scan(reader);

        Assert.True(object.ReferenceEquals(indexes.EntityNames[10], indexes.EntityNames[11]));
        Assert.True(object.ReferenceEquals(indexes.Entities[12].EntityType, indexes.Entities[13].EntityType));
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
