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

            logger.LogInformation(
                "ProveedorCertificado — resultado de la descarga S3. clave={Clave}, encontrado={Encontrado}, bytes={Bytes}.",
                certificado.Datos.RutaAlmacenamiento, pfxBytes is not null, pfxBytes?.Length ?? 0);

            // Log crudo: primeros/últimos bytes en hex del .pfx tal cual llegó de S3 — un PKCS12 válido
            // arranca con la secuencia ASN.1 "30" (SEQUENCE); si en Azure llegara distinto a lo que se ve
            // acá o distinto de lo descargado en la prueba local, sería evidencia de que el archivo se
            // corrompe en tránsito (proxy/encoding), no un problema de contraseña.
            if (pfxBytes is not null)
            {
                var prefijo = Convert.ToHexString(pfxBytes.AsSpan(0, Math.Min(32, pfxBytes.Length)));
                var sufijo = Convert.ToHexString(pfxBytes.AsSpan(Math.Max(0, pfxBytes.Length - 16)));
                logger.LogWarning(
                    "ProveedorCertificado — .pfx crudo: bytes={Bytes}, primeros32Hex={Prefijo}, ultimos16Hex={Sufijo}.",
                    pfxBytes.Length, prefijo, sufijo);
            }

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

                logger.LogInformation(
                    "ProveedorCertificado — buscando credencial {TipoCredencial} (idInquilino={IdInquilino}, idEmpresa={IdEmpresa}).",
                    TipoCredencialClaveCertificado, idInquilino, idEmpresa);

                var credencial = await credencialRepositorio.ObtenerPorTipoAsync(
                    idInquilino, idEmpresa, TipoCredencialClaveCertificado, cancellationToken);
                if (credencial.IdTipoMensaje != TipoMensaje.Exito || credencial.Datos is null)
                {
                    logger.LogWarning(
                        "ProveedorCertificado — no se encontró la credencial {TipoCredencial}: {Mensaje}",
                        TipoCredencialClaveCertificado, credencial.Mensaje);
                    return new ResultadoOperacion<X509Certificate2>(credencial.IdTipoMensaje, credencial.Mensaje, default);
                }

                // Log crudo de lo que efectivamente se lee de CREDENCIALES_INQUILINO antes de intentar
                // descifrarlo — hex completo (son pocos bytes: ValorCifrado suele ser tan largo como la
                // contraseña, Nonce=12, Tag=16), para poder reproducir el descifrado exacto fuera de la app
                // si algo falla, igual que se hizo manualmente para diagnosticar este mismo problema antes.
                logger.LogWarning(
                    "ProveedorCertificado — credencial cruda: valorCifradoHex={ValorCifradoHex}, nonceHex={NonceHex}, tagHex={TagHex}.",
                    Convert.ToHexString(credencial.Datos.ValorCifrado), Convert.ToHexString(credencial.Datos.Nonce), Convert.ToHexString(credencial.Datos.Tag));

                var clave = await cifradoServicio.DescifrarAsync(
                    idInquilino, credencial.Datos.ValorCifrado, credencial.Datos.Nonce, credencial.Datos.Tag, cancellationToken);
                if (clave.IdTipoMensaje != TipoMensaje.Exito || clave.Datos is null)
                {
                    logger.LogWarning("ProveedorCertificado — falló al descifrar la clave del certificado: {Mensaje}", clave.Mensaje);
                    return new ResultadoOperacion<X509Certificate2>(clave.IdTipoMensaje, clave.Mensaje, default);
                }

                // Log crudo del resultado descifrado — bytes UTF8 en hex (no el texto plano en claro) más
                // longitud en caracteres, para poder comparar byte a byte contra una prueba local sin tener
                // que loguear la contraseña real en texto.
                var claveBytesUtf8 = System.Text.Encoding.UTF8.GetBytes(clave.Datos);
                logger.LogWarning(
                    "ProveedorCertificado — clave descifrada: longitudCaracteres={LongitudCaracteres}, bytesUtf8Hex={BytesUtf8Hex}.",
                    clave.Datos.Length, Convert.ToHexString(claveBytesUtf8));

                // Diagnóstico previo al intento: si WEBSITE_LOAD_USER_PROFILE realmente cargó un perfil de
                // usuario real para este proceso, ApplicationData/UserProfile deberían resolver a una ruta
                // real (algo bajo C:\Windows\system32\config\systemprofile o similar en el sandbox de Azure
                // sin perfil, vs. una ruta de usuario real si el setting funcionó). Se loguea antes del
                // intento porque si LoadPkcs12 falla, igual queremos ver esto — no depende de si la carga
                // tuvo éxito o no.
                logger.LogWarning(
                    "ProveedorCertificado — diagnóstico de entorno antes de cargar el .pfx: usuario={Usuario}, " +
                    "ApplicationData='{ApplicationData}', UserProfile='{UserProfile}', LocalApplicationData='{LocalApplicationData}', " +
                    "TEMP={Temp}.",
                    Environment.UserName,
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Path.GetTempPath());

                // Punto de falla frecuente al cambiar de entorno: EphemeralKeySet + el proveedor de
                // criptografía difieren entre Windows (dev) y Linux (p.ej. Azure App Service Linux), y un
                // .pfx corrupto/con clave incorrecta también revienta acá con CryptographicException — antes
                // no había forma de distinguir estos casos del resto de la cadena sin loguear el tipo real
                // de excepción. HResult se agrega porque el Message ("The system cannot find the file
                // specified") es genérico y engañoso — el código real detrás puede señalar la causa exacta
                // (p.ej. un error de perfil/CryptoAPI específico) mejor que el texto.
                try
                {
                    x509 = X509CertificateLoader.LoadPkcs12(
                        pfxBytes, clave.Datos, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
                }
                catch (CryptographicException ex)
                {
                    logger.LogError(
                        ex, "ProveedorCertificado — falló al cargar el .pfx (idCertificado={IdCertificado}, clave S3={Clave}, HResult=0x{HResult:X8}).",
                        idCertificado, certificado.Datos.RutaAlmacenamiento, ex.HResult);

                    // Segundo intento, sin EphemeralKeySet, solo para el log — si este también falla, se
                    // reporta también su excepción/HResult (puede diferir del de arriba y acotar más la
                    // causa real); el resultado que se devuelve sigue siendo el error del primer intento.
                    try
                    {
                        using var x509Alternativo = X509CertificateLoader.LoadPkcs12(
                            pfxBytes, clave.Datos, X509KeyStorageFlags.Exportable);
                        logger.LogWarning(
                            "ProveedorCertificado — el segundo intento (sin EphemeralKeySet) SÍ cargó el certificado. HasPrivateKey={HasPrivateKey}.",
                            x509Alternativo.HasPrivateKey);
                    }
                    catch (CryptographicException exAlternativo)
                    {
                        logger.LogWarning(
                            "ProveedorCertificado — el segundo intento (sin EphemeralKeySet) también falló: {Tipo}: {Mensaje} (HResult=0x{HResult:X8}).",
                            exAlternativo.GetType().Name, exAlternativo.Message, exAlternativo.HResult);
                    }

                    return ResultadoOperacion<X509Certificate2>.DeErrorSistema(
                        $"No se pudo cargar el certificado: {ex.Message}");
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