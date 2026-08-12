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
    IConfiguration configuracion,
    ILogger<ProveedorCertificadoServicio> logger) : IProveedorCertificadoServicio
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
            logger.LogInformation(
                "ProveedorCertificado — usando certificado en caché. claveCache={ClaveCache}, sujeto={Sujeto}, huellaDigital={HuellaDigital}, validoHasta={ValidoHasta:o}.",
                claveCache, x509Cacheado.Subject, x509Cacheado.Thumbprint, x509Cacheado.NotAfter);
            return ResultadoOperacion<X509Certificate2>.DeExito("Certificado cargado correctamente (caché).", x509Cacheado);
        }

        try
        {
            var certificado = await certificadoRepositorio.ObtenerAsync(idInquilino, idCertificado, cancellationToken);
            if (certificado.IdTipoMensaje != TipoMensaje.Exito || certificado.Datos is null)
            {
                return new ResultadoOperacion<X509Certificate2>(certificado.IdTipoMensaje, certificado.Mensaje, default);
            }

            logger.LogInformation(
                "ProveedorCertificado — descargando de S3. bucket={Bucket}, clave={Clave}.",
                BucketName, certificado.Datos.RutaAlmacenamiento);

            var pfxBytes = await DescargarDeS3Async(certificado.Datos.RutaAlmacenamiento, cancellationToken);

            X509Certificate2 x509;

            if (entorno.IsDevelopment() && pfxBytes is null)
            {
                logger.LogWarning(
                    "ProveedorCertificado — no se encontró el .pfx en S3 (clave={Clave}), generando certificado autofirmado de desarrollo (EnvironmentName={EnvironmentName}).",
                    certificado.Datos.RutaAlmacenamiento, entorno.EnvironmentName);
                x509 = GenerarCertificadoDev();
            }
            else
            {
                if (pfxBytes is null)
                {
                    logger.LogWarning(
                        "ProveedorCertificado — no se encontró el certificado en S3: bucket={Bucket}, clave={Clave}.",
                        BucketName, certificado.Datos.RutaAlmacenamiento);
                    return ResultadoOperacion<X509Certificate2>.DeErrorSistema(
                        $"No se encontró el certificado en S3: {certificado.Datos.RutaAlmacenamiento}.");
                }

                var credencial = await credencialRepositorio.ObtenerPorTipoAsync(
                    idInquilino, idEmpresa, TipoCredencialClaveCertificado, cancellationToken);
                if (credencial.IdTipoMensaje != TipoMensaje.Exito || credencial.Datos is null)
                {
                    logger.LogWarning(
                        "ProveedorCertificado — no se encontró la credencial {TipoCredencial}: {Mensaje}",
                        TipoCredencialClaveCertificado, credencial.Mensaje);
                    return new ResultadoOperacion<X509Certificate2>(credencial.IdTipoMensaje, credencial.Mensaje, default);
                }

                var clave = await cifradoServicio.DescifrarAsync(
                    idInquilino, credencial.Datos.ValorCifrado, credencial.Datos.Nonce, credencial.Datos.Tag, cancellationToken);
                if (clave.IdTipoMensaje != TipoMensaje.Exito || clave.Datos is null)
                {
                    logger.LogWarning("ProveedorCertificado — falló al descifrar la clave del certificado: {Mensaje}", clave.Mensaje);
                    return new ResultadoOperacion<X509Certificate2>(clave.IdTipoMensaje, clave.Mensaje, default);
                }

                // X509KeyStorageFlags es un concepto de Windows CAPI/CNG (contenedores de clave con nombre).
                // En Unix, X509CertificateLoader va sobre OpenSSL, que no tiene ese almacén — los 3 flags son
                // no-op ahí (la clave siempre se materializa en memoria, vía EVP_PKEY), así que encadenar los
                // 3 intentos no cambia nada, solo agrega ruido de log. OperatingSystem.IsWindows() detecta el
                // SO real del proceso en ejecución (no dónde se compiló) — este mismo código corre tanto en
                // Windows (desarrollo local) como en Linux (AWS, destino de producción).
                //
                // En Windows: MachineKeySet y UserKeySet fuerzan a materializar la clave en un contenedor de
                // clave real (en vez de dejar que Windows resuelva un backing store efímero por su cuenta),
                // lo que evita el HResult 0x80070002 visto en Azure App Service — la resolución de clave
                // efímera está bloqueada en ese sandbox específicamente (ver investigación previa). Ninguno
                // pide Exportable — el firmador solo necesita acceso a la clave privada, no exportarla.
                var intentos = OperatingSystem.IsWindows()
                    ? new (string Nombre, X509KeyStorageFlags Flags)[]
                      {
                          ("MachineKeySet", X509KeyStorageFlags.MachineKeySet),
                          ("UserKeySet", X509KeyStorageFlags.UserKeySet),
                          ("EphemeralKeySet", X509KeyStorageFlags.EphemeralKeySet),
                      }
                    : new (string Nombre, X509KeyStorageFlags Flags)[]
                      {
                          ("EphemeralKeySet", X509KeyStorageFlags.EphemeralKeySet),
                      };

                Exception? ultimoError = null;
                x509 = null!;
                foreach (var (nombre, flags) in intentos)
                {
                    try
                    {
                        x509 = X509CertificateLoader.LoadPkcs12(pfxBytes, clave.Datos, flags);
                        ultimoError = null;
                        break;
                    }
                    catch (Exception ex)
                    {
                        ultimoError = ex;
                        logger.LogError("ProveedorCertificado — falló LoadPkcs12 con flag={Flag}. tipo={Tipo}, mensaje={Mensaje}, HResult=0x{HResult:X8}.",
                            nombre, ex.GetType().FullName, ex.Message, ex.HResult);
                    }
                }

                if (ultimoError is not null)
                {
                    logger.LogError("ProveedorCertificado — falló al cargar el .pfx con los {Cantidad} flags probados. idCertificado={IdCertificado}.",
                        intentos.Length, idCertificado);
                    if (ultimoError.InnerException is not null)
                    {
                        logger.LogError("ProveedorCertificado — InnerException tipo: {Tipo}.", ultimoError.InnerException.GetType().FullName);
                        logger.LogError("ProveedorCertificado — InnerException mensaje: {Mensaje}", ultimoError.InnerException.Message);
                    }

                    return ResultadoOperacion<X509Certificate2>.DeErrorSistema(
                        $"No se pudo cargar el certificado: {ultimoError.Message}");
                }

                // Confirma que el .pfx descargado de S3 (no uno de caché ni el autofirmado de dev) es
                // realmente el que se usa a partir de acá — sujeto/huella digital identifican
                // inequívocamente cuál certificado real cargó, y tieneClavePrivada confirma que
                // GetRSAPrivateKey() (usado más adelante por FirmadorXmlServicio) va a poder acceder a
                // ella, no solo que el objeto X509Certificate2 se construyó sin tirar excepción.
                logger.LogInformation(
                    "ProveedorCertificado — certificado cargado desde S3. sujeto={Sujeto}, huellaDigital={HuellaDigital}, " +
                    "validoDesde={ValidoDesde:o}, validoHasta={ValidoHasta:o}, tieneClavePrivada={TieneClavePrivada}.",
                    x509.Subject, x509.Thumbprint, x509.NotBefore, x509.NotAfter, x509.HasPrivateKey);
            }

            cache.Set(claveCache, x509, DuracionCache);

            return ResultadoOperacion<X509Certificate2>.DeExito("Certificado cargado correctamente.", x509);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "ProveedorCertificado — excepción no controlada (idInquilino={IdInquilino}, idEmpresa={IdEmpresa}, idCertificado={IdCertificado}).",
                idInquilino, idEmpresa, idCertificado);
            return ResultadoOperacion<X509Certificate2>.DeErrorSistema(ex.Message);
        }
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