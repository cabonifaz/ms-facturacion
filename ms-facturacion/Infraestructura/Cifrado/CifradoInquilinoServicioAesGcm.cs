using System.Security.Cryptography;
using System.Text;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Cifrado;

/// AES-256-GCM directo bajo una única llave maestra (Cifrado:LlaveMaestraBase64, fuera de la base de
/// datos) — sin envelope encryption ni llave de datos por tenant (ver LLAVES_CIFRADO_INQUILINO, removida:
/// esa capa no aportaba nada que la llave maestra fuera de la BD no diera ya, para el alcance actual de
/// un solo tipo de credencial por empresa).
public sealed class CifradoInquilinoServicioAesGcm(IConfiguration configuracion) : ICifradoInquilinoServicio
{
    private const int TamanoNonce = 12;
    private const int TamanoTag = 16;

    private byte[] LlaveMaestra => Convert.FromBase64String(
        configuracion["Cifrado:LlaveMaestraBase64"]
        ?? throw new InvalidOperationException("No se configuró 'Cifrado:LlaveMaestraBase64'."));

    public Task<ResultadoOperacion<CredencialCifrada>> CifrarAsync(
        int idInquilino, string valorPlano, CancellationToken cancellationToken)
    {
        var textoPlanoBytes = Encoding.UTF8.GetBytes(valorPlano);
        var nonce = RandomNumberGenerator.GetBytes(TamanoNonce);
        var tag = new byte[TamanoTag];
        var cifrado = new byte[textoPlanoBytes.Length];

        using (var aesGcm = new AesGcm(LlaveMaestra, TamanoTag))
        {
            aesGcm.Encrypt(nonce, textoPlanoBytes, cifrado, tag);
        }

        var resultado = new CredencialCifrada(cifrado, nonce, tag);
        return Task.FromResult(ResultadoOperacion<CredencialCifrada>.DeExito("Valor cifrado correctamente.", resultado));
    }

    public Task<ResultadoOperacion<string>> DescifrarAsync(
        int idInquilino, byte[] valorCifrado, byte[] nonce, byte[] tag, CancellationToken cancellationToken)
    {
        var textoPlanoBytes = new byte[valorCifrado.Length];
        using (var aesGcm = new AesGcm(LlaveMaestra, TamanoTag))
        {
            aesGcm.Decrypt(nonce, valorCifrado, tag, textoPlanoBytes);
        }

        return Task.FromResult(ResultadoOperacion<string>.DeExito("Valor descifrado correctamente.", Encoding.UTF8.GetString(textoPlanoBytes)));
    }
}
