using ExportDocGen.Auth;

namespace ExportDocGen.Tests;

public class PasswordHashTests
{
    [Fact]
    public void Verifies_the_correct_password()
    {
        var encoded = PasswordHash.Create("correct horse battery staple");
        Assert.True(PasswordHash.Verify("correct horse battery staple", encoded));
    }

    [Fact]
    public void Rejects_the_wrong_password()
    {
        var encoded = PasswordHash.Create("s3cret");
        Assert.False(PasswordHash.Verify("S3cret", encoded));
        Assert.False(PasswordHash.Verify("", encoded));
        Assert.False(PasswordHash.Verify("s3cret ", encoded));
    }

    [Fact]
    public void Each_hash_is_salted_uniquely()
    {
        Assert.NotEqual(PasswordHash.Create("same"), PasswordHash.Create("same"));
    }

    [Fact]
    public void Encoded_form_is_the_documented_shape()
    {
        var parts = PasswordHash.Create("x").Split('.');
        Assert.Equal(4, parts.Length);
        Assert.Equal("v1", parts[0]);
        Assert.Equal("210000", parts[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("v1.210000.only-three-parts")]
    [InlineData("v2.210000.AAAA.BBBB")]
    [InlineData("v1.abc.AAAA.BBBB")]
    [InlineData("v1.210000.@@@.BBBB")]
    public void Malformed_encoded_input_returns_false_not_throws(string encoded)
    {
        Assert.False(PasswordHash.Verify("anything", encoded));
    }

    [Fact]
    public void A_tampered_hash_does_not_verify()
    {
        var encoded = PasswordHash.Create("password");
        var parts = encoded.Split('.');
        var flipped = Convert.FromBase64String(parts[3]);
        flipped[0] ^= 0x01;
        var tampered = $"{parts[0]}.{parts[1]}.{parts[2]}.{Convert.ToBase64String(flipped)}";
        Assert.False(PasswordHash.Verify("password", tampered));
    }
}
