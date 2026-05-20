using Pots.Domain;
using Pots.Domain.Entities;
using Xunit;

namespace Pots.Domain.Tests;

public sealed class GrantUpgradeRequestTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Patient = Guid.NewGuid();
    private static readonly Guid Grantee = Guid.NewGuid();

    private static PatientGrant ViewerGrant() =>
        PatientGrant.Issue(Patient, Grantee, GrantRole.Viewer, Owner, Owner);

    [Fact]
    public void Create_OnActiveViewerGrant_StartsPending()
    {
        var grant = ViewerGrant();
        var req = GrantUpgradeRequest.Create(grant, Grantee, message: "Cuando estoy mal, necesito ayuda");

        Assert.True(req.IsPending);
        Assert.Equal(grant.Id, req.GrantId);
        Assert.Equal(Grantee, req.RequesterUserId);
        Assert.Equal(Patient, req.PatientId);
        Assert.Equal("Cuando estoy mal, necesito ayuda", req.Message);
        Assert.Null(req.ResolvedAt);
        Assert.Null(req.ResolvedByUserId);
    }

    [Fact]
    public void Create_OnEditorGrant_Throws()
    {
        var editor = PatientGrant.Issue(Patient, Grantee, GrantRole.Editor, Owner, Owner);
        var ex = Assert.Throws<DomainException>(() => GrantUpgradeRequest.Create(editor, Grantee, null));
        Assert.Contains("Viewer", ex.Message);
    }

    [Fact]
    public void Create_OnRevokedGrant_Throws()
    {
        var grant = ViewerGrant();
        grant.Revoke(Owner);
        Assert.Throws<DomainException>(() => GrantUpgradeRequest.Create(grant, Grantee, null));
    }

    [Fact]
    public void Create_RejectsNonGranteeRequester()
    {
        var grant = ViewerGrant();
        var stranger = Guid.NewGuid();
        Assert.Throws<DomainException>(() => GrantUpgradeRequest.Create(grant, stranger, null));
    }

    [Fact]
    public void Create_TrimsAndAllowsEmptyMessage()
    {
        var grant = ViewerGrant();
        var req = GrantUpgradeRequest.Create(grant, Grantee, "   ");
        Assert.Null(req.Message);

        var req2 = GrantUpgradeRequest.Create(ViewerGrant(), Grantee, "  hola  ");
        Assert.Equal("hola", req2.Message);
    }

    [Fact]
    public void Create_RejectsMessageOver500Chars()
    {
        var grant = ViewerGrant();
        var tooLong = new string('a', 501);
        Assert.Throws<DomainException>(() => GrantUpgradeRequest.Create(grant, Grantee, tooLong));
    }

    [Fact]
    public void Approve_TransitionsPendingToApproved()
    {
        var req = GrantUpgradeRequest.Create(ViewerGrant(), Grantee, null);
        req.Approve(Owner);
        Assert.Equal(GrantUpgradeRequestStatus.Approved, req.Status);
        Assert.NotNull(req.ResolvedAt);
        Assert.Equal(Owner, req.ResolvedByUserId);
    }

    [Fact]
    public void Approve_AlreadyResolved_Throws()
    {
        var req = GrantUpgradeRequest.Create(ViewerGrant(), Grantee, null);
        req.Approve(Owner);
        Assert.Throws<DomainException>(() => req.Approve(Owner));
    }

    [Fact]
    public void Deny_TransitionsPendingToDenied()
    {
        var req = GrantUpgradeRequest.Create(ViewerGrant(), Grantee, null);
        req.Deny(Owner);
        Assert.Equal(GrantUpgradeRequestStatus.Denied, req.Status);
        Assert.Equal(Owner, req.ResolvedByUserId);
    }

    [Fact]
    public void Deny_AfterApprove_Throws()
    {
        var req = GrantUpgradeRequest.Create(ViewerGrant(), Grantee, null);
        req.Approve(Owner);
        Assert.Throws<DomainException>(() => req.Deny(Owner));
    }

    [Fact]
    public void Cancel_OnlyByRequester()
    {
        var req = GrantUpgradeRequest.Create(ViewerGrant(), Grantee, null);
        Assert.Throws<DomainException>(() => req.Cancel(Owner));   // owner can't cancel
        req.Cancel(Grantee);
        Assert.Equal(GrantUpgradeRequestStatus.Cancelled, req.Status);
    }

    [Fact]
    public void Cancel_AfterResolution_Throws()
    {
        var req = GrantUpgradeRequest.Create(ViewerGrant(), Grantee, null);
        req.Deny(Owner);
        Assert.Throws<DomainException>(() => req.Cancel(Grantee));
    }

    [Fact]
    public void Approve_RequiresResolver()
    {
        var req = GrantUpgradeRequest.Create(ViewerGrant(), Grantee, null);
        Assert.Throws<DomainException>(() => req.Approve(Guid.Empty));
    }

    // PatientGrant.UpgradeToEditor — paired with the approval flow.

    [Fact]
    public void UpgradeToEditor_OnViewer_MovesToEditor()
    {
        var grant = ViewerGrant();
        grant.UpgradeToEditor();
        Assert.Equal(GrantRole.Editor, grant.Role);
        Assert.True(grant.IsActive);
    }

    [Fact]
    public void UpgradeToEditor_OnEditor_IsIdempotent()
    {
        var grant = PatientGrant.Issue(Patient, Grantee, GrantRole.Editor, Owner, Owner);
        grant.UpgradeToEditor();
        Assert.Equal(GrantRole.Editor, grant.Role);
    }

    [Fact]
    public void UpgradeToEditor_OnRevokedGrant_Throws()
    {
        var grant = ViewerGrant();
        grant.Revoke(Owner);
        Assert.Throws<DomainException>(() => grant.UpgradeToEditor());
    }
}
