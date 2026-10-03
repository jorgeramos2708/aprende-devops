namespace DevOpsPlatform.Tests;

using DevOpsPlatform.Infrastructure.Services;
using Xunit;

public class PasswordHasherTests
{
    private readonly PasswordHasher _sut = new();

    [Fact]
    public void Hash_Verify_Roundtrip_Ok()
    {
        var hash = _sut.Hash("Secreta123");
        Assert.True(_sut.Verify("Secreta123", hash));
    }

    [Fact]
    public void Verify_WrongPassword_False()
    {
        var hash = _sut.Hash("Secreta123");
        Assert.False(_sut.Verify("otra-clave", hash));
    }

    [Fact]
    public void Hash_Twice_DifferentSalt()
    {
        Assert.NotEqual(_sut.Hash("misma"), _sut.Hash("misma"));
    }

    [Fact]
    public void Verify_Garbage_False()
    {
        Assert.False(_sut.Verify("x", "no-es-un-hash"));
        Assert.False(_sut.Verify("x", ""));
    }
}
