using Atlas.Catalog.Application;
using Xunit;

namespace Atlas.Catalog.Api.Tests;

public class TokenProbeTests
{
    [Fact]
    public void Roundtrip()
    {
        var secret = "atlas-dev-approval-secret";
        var token = ApprovalToken.Issue(
            new ApprovalToken.Payload("approver-1", "11111111-2222-3333-4444-555555555555", 3,
                DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()), secret);
        var v = ApprovalToken.Validate(token, secret, "11111111-2222-3333-4444-555555555555", 3, DateTimeOffset.UtcNow);
        Assert.True(v is not null, "roundtrip falhou");
    }
}
