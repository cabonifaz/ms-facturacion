using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Caching.Memory;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Infraestructura.Xml;

/// Depende directamente de los mismos dos puertos que DescifrarCredencialPorTipoCasoDeUso usa
/// (ICredencialInquilinoRepositorio + ICifradoInquilinoServicio) en vez de depender de ese Caso de Uso:
/// un Adaptador de Infraestructura no debe depender de clases concretas de Aplicacion, solo de Puertos.
///
/// Cachea el certificado ya cargado (X509Certificate2) en memoria, por inquilino+empresa+certificado —
/// cargar/descifrar en cada transacción sería un round-trip innecesario (a disco hoy, a S3 si se migra
/// CERTIFICADOS.RutaAlmacenamiento más adelante) para un archivo que no cambia salvo rotación explícita
/// (SP_Certificado_Actualizar). Expiración absoluta de 4 horas desde que se cachea, se use o no en ese
/// tiempo — no se renueva por actividad. Pasadas las 4 horas se descarta sola, sin limpieza manual; la
/// siguiente vez que alguien la pida simplemente se vuelve a cargar. IMemoryCache es singleton — se
/// inyecta sin problema en este Adaptador aunque esté registrado Scoped.
///
/// En Development, si el .pfx/.p12 real no existe en RutaAlmacenamiento, se genera un certificado
/// autofirmado desechable en memoria en su lugar — para poder probar el flujo de firma localmente sin
/// depender de un certificado SUNAT real. En cualquier otro entorno (preprod/prod) el archivo real es
/// obligatorio, sin excepción.
public sealed class ProveedorCertificadoServicio(
    ICertificadoRepositorio certificadoRepositorio,
    ICredencialInquilinoRepositorio credencialRepositorio,
    ICifradoInquilinoServicio cifradoServicio,
    IMemoryCache cache,
    IHostEnvironment entorno) : IProveedorCertificadoServicio
{
    private const string TipoCredencialClaveCertificado = "ClaveCertificado";
    private static readonly TimeSpan DuracionCache = TimeSpan.FromHours(4);

    public async Task<ResultadoOperacion<X509Certificate2>> ObtenerAsync(
        int idInquilino, int idEmpresa, int idCertificado, CancellationToken cancellationToken)
    {
        var claveCache = $"certificado:{idInquilino}:{idEmpresa}:{idCertificado}";

        if (cache.TryGetValue(claveCache, out X509Certificate2? x509Cacheado) && x509Cacheado is not null)
        {
            return ResultadoOperacion<X509Certificate2>.DeExito("Certificado cargado correctamente (caché).", x509Cacheado);
        }

        var certificado = await certificadoRepositorio.ObtenerAsync(idInquilino, idCertificado, cancellationToken);
        if (certificado.IdTipoMensaje != TipoMensaje.Exito || certificado.Datos is null)
        {
            return new ResultadoOperacion<X509Certificate2>(certificado.IdTipoMensaje, certificado.Mensaje, default);
        }

        X509Certificate2 x509;

        if (entorno.IsDevelopment() && !File.Exists(certificado.Datos.RutaAlmacenamiento))
        {
            x509 = GenerarCertificadoDev();
        }
        else
        {
            var credencial = await credencialRepositorio.ObtenerPorTipoAsync(
                idInquilino, idEmpresa, TipoCredencialClaveCertificado, cancellationToken);
            if (credencial.IdTipoMensaje != TipoMensaje.Exito || credencial.Datos is null)
            {
                return new ResultadoOperacion<X509Certificate2>(credencial.IdTipoMensaje, credencial.Mensaje, default);
            }

            var clave = await cifradoServicio.DescifrarAsync(
                idInquilino, credencial.Datos.ValorCifrado, credencial.Datos.Nonce, credencial.Datos.Tag, cancellationToken);
            if (clave.IdTipoMensaje != TipoMensaje.Exito || clave.Datos is null)
            {
                return new ResultadoOperacion<X509Certificate2>(clave.IdTipoMensaje, clave.Mensaje, default);
            }

            x509 = X509CertificateLoader.LoadPkcs12FromFile(
                certificado.Datos.RutaAlmacenamiento, clave.Datos,
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
        }

        cache.Set(claveCache, x509, DuracionCache);

        return ResultadoOperacion<X509Certificate2>.DeExito("Certificado cargado correctamente.", x509);
    }

    /// Certificado autofirmado desechable, solo para ejercitar FirmadorXmlServicio (requiere RSA +
    /// clave privada) en desarrollo local sin un .pfx real de SUNAT.
    private static X509Certificate2 GenerarCertificadoDev()
    {
        using var rsa = RSA.Create(2048);
        var solicitud = new CertificateRequest(
            "CN=ms-facturacion-dev", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }
}
