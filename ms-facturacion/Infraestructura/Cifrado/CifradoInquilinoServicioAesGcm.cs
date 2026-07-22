using System.Security.Cryptography;
using System.Text;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Cifrado;

/// Envelope encryption: una llave maestra (fuera de BD, en configuración) cifra la llave de datos de
/// cada inquilino (LLAVES_CIFRADO_INQUILINO); esa llave de datos, ya en claro solo en memoria, cifra/
/// descifra los valores individuales de CREDENCIALES_INQUILINO. Aprovisiona la primera llave de datos
/// de un inquilino automáticamente si todavía no existe.
public sealed class CifradoInquilinoServicioAesGcm(
    ILlaveCifradoInquilinoRepositorio llaveRepositorio, IConfiguration configuracion) : ICifradoInquilinoServicio
{
    private const string UsuarioSistemaCifrado = "sistema-cifrado";
    private const string Algoritmo = "AES-256-GCM";
    private const int TamanoNonce = 12;
    private const int TamanoTag = 16;
    private const int TamanoLlaveDatos = 32;

    private byte[] LlaveMaestra => Convert.FromBase64String(
        configuracion["Cifrado:LlaveMaestraBase64"]
        ?? throw new InvalidOperationException("No se configuró 'Cifrado:LlaveMaestraBase64'."));

    public async Task<ResultadoOperacion<CredencialCifrada>> CifrarAsync(
        int idInquilino, string valorPlano, CancellationToken cancellationToken)
    {
        var llaveActiva = await ObtenerOAprovisionarLlaveActivaAsync(idInquilino, cancellationToken);
        if (llaveActiva.IdTipoMensaje != TipoMensaje.Exito || llaveActiva.Datos is null)
        {
            return new ResultadoOperacion<CredencialCifrada>(llaveActiva.IdTipoMensaje, llaveActiva.Mensaje, default);
        }

        var llaveDatos = DescifrarLlaveDatos(llaveActiva.Datos);

        var textoPlanoBytes = Encoding.UTF8.GetBytes(valorPlano);
        var nonce = RandomNumberGenerator.GetBytes(TamanoNonce);
        var tag = new byte[TamanoTag];
        var cifrado = new byte[textoPlanoBytes.Length];

        using (var aesGcm = new AesGcm(llaveDatos, TamanoTag))
        {
            aesGcm.Encrypt(nonce, textoPlanoBytes, cifrado, tag);
        }

        var resultado = new CredencialCifrada(cifrado, nonce, tag, llaveActiva.Datos.VersionLlave);
        return ResultadoOperacion<CredencialCifrada>.DeExito("Valor cifrado correctamente.", resultado);
    }

    public async Task<ResultadoOperacion<string>> DescifrarAsync(
        int idInquilino, int versionLlave, byte[] valorCifrado, byte[] nonce, byte[] tag, CancellationToken cancellationToken)
    {
        var llave = await llaveRepositorio.ObtenerPorVersionAsync(idInquilino, versionLlave, cancellationToken);
        if (llave.IdTipoMensaje != TipoMensaje.Exito || llave.Datos is null)
        {
            return new ResultadoOperacion<string>(llave.IdTipoMensaje, llave.Mensaje, default);
        }

        var llaveDatos = DescifrarLlaveDatos(llave.Datos);

        var textoPlanoBytes = new byte[valorCifrado.Length];
        using (var aesGcm = new AesGcm(llaveDatos, TamanoTag))
        {
            aesGcm.Decrypt(nonce, valorCifrado, tag, textoPlanoBytes);
        }

        return ResultadoOperacion<string>.DeExito("Valor descifrado correctamente.", Encoding.UTF8.GetString(textoPlanoBytes));
    }

    private async Task<ResultadoOperacion<LlaveCifradoInquilino>> ObtenerOAprovisionarLlaveActivaAsync(
        int idInquilino, CancellationToken cancellationToken)
    {
        var activa = await llaveRepositorio.ObtenerActivaAsync(idInquilino, cancellationToken);
        if (activa.IdTipoMensaje != TipoMensaje.ReglaDeNegocio)
        {
            // Éxito (ya existe) o ErrorSistema (falla real) — en ambos casos no hay nada más que hacer aquí.
            return activa;
        }

        // ReglaDeNegocio == "no existe llave activa todavía" (ver SP_LlaveCifradoInquilino_ObtenerActiva) → aprovisionar.
        var llaveDatosNueva = RandomNumberGenerator.GetBytes(TamanoLlaveDatos);
        var nonceLlave = RandomNumberGenerator.GetBytes(TamanoNonce);
        var tagLlave = new byte[TamanoTag];
        var llaveCifradaBytes = new byte[llaveDatosNueva.Length];

        using (var aesGcmMaestra = new AesGcm(LlaveMaestra, TamanoTag))
        {
            aesGcmMaestra.Encrypt(nonceLlave, llaveDatosNueva, llaveCifradaBytes, tagLlave);
        }

        var insertar = await llaveRepositorio.InsertarAsync(
            UsuarioSistemaCifrado, idInquilino, 1, llaveCifradaBytes, nonceLlave, tagLlave, Algoritmo, true, cancellationToken);

        if (insertar.IdTipoMensaje != TipoMensaje.Exito)
        {
            return new ResultadoOperacion<LlaveCifradoInquilino>(insertar.IdTipoMensaje, insertar.Mensaje, default);
        }

        return await llaveRepositorio.ObtenerActivaAsync(idInquilino, cancellationToken);
    }

    private byte[] DescifrarLlaveDatos(LlaveCifradoInquilino llave)
    {
        var llaveDatos = new byte[llave.LlaveDatosCifrada.Length];
        using var aesGcmMaestra = new AesGcm(LlaveMaestra, TamanoTag);
        aesGcmMaestra.Decrypt(llave.Nonce, llave.LlaveDatosCifrada, llave.Tag, llaveDatos);
        return llaveDatos;
    }
}
