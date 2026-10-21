using FarmaGestion.Services;
using Xunit;

namespace FarmaGestion.Tests;

public class AuthServiceTests
{
    [Fact]
    public void HashPassword_ConPasswordCorrecta_Verifica()
    {
        var salt = AuthService.GenerarSalt();
        var hash = AuthService.HashPassword("MiClave123", salt);

        Assert.True(AuthService.VerificarPassword("MiClave123", hash, salt));
    }

    [Fact]
    public void HashPassword_ConPasswordIncorrecta_NoVerifica()
    {
        var salt = AuthService.GenerarSalt();
        var hash = AuthService.HashPassword("MiClave123", salt);

        Assert.False(AuthService.VerificarPassword("ClaveEquivocada", hash, salt));
    }

    [Fact]
    public void GenerarSalt_ProduceValoresDistintos()
    {
        Assert.NotEqual(AuthService.GenerarSalt(), AuthService.GenerarSalt());
    }

    [Fact]
    public void HashPassword_ConSaltsDistintos_ProduceHashesDistintos()
    {
        var hash1 = AuthService.HashPassword("igual", AuthService.GenerarSalt());
        var hash2 = AuthService.HashPassword("igual", AuthService.GenerarSalt());

        Assert.NotEqual(hash1, hash2);
    }
}
