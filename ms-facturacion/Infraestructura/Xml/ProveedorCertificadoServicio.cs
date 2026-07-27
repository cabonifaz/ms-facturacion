using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Amazon.S3;
using Microsoft.Extensions.Caching.Memory;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Infraestructura.Xml;

/// Depende directamente de los mismos dos puertos que DescifrarCredencialPorTipoCasoDeUso usa
/// (ICredencialInquilinoRepositorio + ICifradoInquilinoServicio) en vez de depender de ese Caso de Uso:
/// un Adaptador de Infraestructura no debe depender de clases concretas de Aplicacion, solo de Puertos.
/// CERTIFICADOS.RutaAlmacenamiento es una clave de S3 (no una ruta de disco) — mismo bucket que
/// AlmacenamientoArchivosS3Servicio (AWS:BucketName), IAmazonS3 inyectado directamente igual que ese
/// Adaptador (no pasa por IAlmacenamientoArchivosServicio porque ese puerto solo sabe guardar, no leer).
///
/// Cachea el certificado ya cargado (X509Certificate2) en memoria, por inquilino+empresa+certificado —
/// descargar/descifrar en cada transacción sería un round-trip innecesario a S3 para un archivo que no
/// cambia salvo rotación explícita (SP_Certificado_Actualizar). Expiración absoluta de 4 horas desde que
/// se cachea, se use o no en ese tiempo — no se renueva por actividad. Pasadas las 4 horas se descarta
/// sola, sin limpieza manual; la siguiente vez que alguien la pida simplemente se vuelve a cargar.
/// IMemoryCache es singleton — se inyecta sin problema en este Adaptador aunque esté registrado Scoped.
///
/// En Development, si el .pfx/.p12 real no existe en S3 (clave no encontrada), se genera un certificado
/// autofirmado desechable en memoria en su lugar — para poder probar el flujo de firma localmente sin
/// depender de un certificado SUNAT real subido a S3. En cualquier otro entorno (preprod/prod) el
/// objeto real es obligatorio, sin excepción.
public sealed class ProveedorCertificadoServicio(
    ICertificadoRepositorio certificadoRepositorio,
    ICredencialInquilinoRepositorio credencialRepositorio,
    ICifradoInquilinoServicio cifradoServicio,
    IMemoryCache cache,
    IHostEnvironment entorno,
    IAmazonS3 s3Cliente,
    IConfiguration configuracion) : IProveedorCertificadoServicio
{
    private const string TipoCredencialClaveCertificado = "ClaveCertificado";
    private static readonly TimeSpan DuracionCache = TimeSpan.FromHours(4);

    private string BucketName => configuracion["AWS:BucketName"]
        ?? throw new InvalidOperationException("No se configuró 'AWS:BucketName'.");

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

        var pfxBytes = await DescargarDeS3Async(certificado.Datos.RutaAlmacenamiento, cancellationToken);

        X509Certificate2 x509;

        if (entorno.IsDevelopment() && pfxBytes is null)
        {
            x509 = GenerarCertificadoDev();
        }
        else
        {
            if (pfxBytes is null)
            {
                return ResultadoOperacion<X509Certificate2>.DeErrorSistema(
                    $"No se encontró el certificado en S3: {certificado.Datos.RutaAlmacenamiento}.");
            }

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

            x509 = X509CertificateLoader.LoadPkcs12(
                pfxBytes, clave.Datos, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
        }

        cache.Set(claveCache, x509, DuracionCache);

        return ResultadoOperacion<X509Certificate2>.DeExito("Certificado cargado correctamente.", x509);
    }

    private async Task<byte[]?> DescargarDeS3Async(string clave, CancellationToken cancellationToken)
    {
        try
        {
            using var respuesta = await s3Cliente.GetObjectAsync(BucketName, clave, cancellationToken);
            using var memoria = new MemoryStream();
            await respuesta.ResponseStream.CopyToAsync(memoria, cancellationToken);
            return memoria.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
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