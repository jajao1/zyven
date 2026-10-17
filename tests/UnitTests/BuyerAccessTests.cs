using Zyven.Infrastructure;
namespace UnitTests;

public class BuyerAccessTests
{
    [Fact]
    public void Code_hash_is_deterministic_and_bound_to_email_and_secret()
    {
        var hasher = new BuyerCodeHasher("a sufficiently long test secret");
        var hash = hasher.Hash("BUYER@EXAMPLE.TEST", "123456");
        Assert.Equal(hash, hasher.Hash("BUYER@EXAMPLE.TEST", "123456"));
        Assert.NotEqual(hash, hasher.Hash("OTHER@EXAMPLE.TEST", "123456"));
        Assert.NotEqual(hash, new BuyerCodeHasher("another test secret").Hash("BUYER@EXAMPLE.TEST", "123456"));
        Assert.DoesNotContain("123456", hash);
    }
}
