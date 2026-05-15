using Pots.Domain.Entities;
using Xunit;

namespace Pots.Domain.Tests;

// Pins the enum names to the SQL policy literals in migration
// EnableRowLevelSecurity (patients_owner_or_editor_update uses g.role = 'Editor').
// Renaming Viewer or Editor without updating the migration is a silent security
// regression — these tests fail fast.
public sealed class GrantRolePinningTests
{
    [Fact]
    public void Viewer_StableName()
    {
        Assert.Equal("Viewer", GrantRole.Viewer.ToString());
    }

    [Fact]
    public void Editor_StableName()
    {
        Assert.Equal("Editor", GrantRole.Editor.ToString());
    }

    [Fact]
    public void UnexpectedRolesDoNotExist()
    {
        var declared = Enum.GetNames<GrantRole>();
        Array.Sort(declared);
        Assert.Equal(new[] { "Editor", "Viewer" }, declared);
    }
}
