using System.Reflection;
using Xunit;

namespace CampaignManager.Core.Tests;

public sealed class CoreAssemblyTests
{
    // Core общий с мобильным приложением (D4): если кто-то снимет IsTrimmable/IsAotCompatible
    // в csproj, анализаторы перестанут ловить рефлексию — и сломается это приложение, а не веб.
    [Fact]
    public void Core_is_marked_trimmable()
    {
        var core = Assembly.Load("CampaignManager.Core");

        Assert.Contains(core.GetCustomAttributes<AssemblyMetadataAttribute>(),
            attribute => attribute is { Key: "IsTrimmable", Value: "True" });
    }
}
