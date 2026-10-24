using Library.Domain;

namespace Library.UnitTests;

public class SmokeTests
{
    [Fact]
    public void Domain_assembly_is_referenced()
    {
        var assemblyName = typeof(DomainAssembly).Assembly.GetName().Name;

        Assert.Equal(DomainAssembly.Name, assemblyName);
    }
}
