using Amazon.S3;
using Amazon.S3.Model;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Infraestructura.Almacenamiento;

/// Adaptador de infraestructura (driven) para IAlmacenamientoArchivosServicio — sube XML/ZIP/CDR a S3.
/// Mismo patrón de conexión que maximlian3_backend/SafetyReport.Handlers/S3UploadService.cs: IAmazonS3
/// inyectado (credenciales estáticas + región resueltas una sola vez en Program.cs), nombre de bucket
/// leído de configuración. El resto del sistema solo conoce IAlmacenamientoArchivosServicio.
public sealed class AlmacenamientoArchivosS3Servicio(
    IAmazonS3 s3Cliente, IConfiguration configuracion, ILogger<AlmacenamientoArchivosS3Servicio> logger) : IAlmacenamientoArchivosServicio
{
    private string BucketName => configuracion["AWS:BucketName"]
        ?? throw new InvalidOperationException("No se configuró 'AWS:BucketName'.");

    public async Task<string> GuardarAsync(string carpeta, string nombreArchivo, byte[] contenido, CancellationToken cancellationToken)
    {
        var clave = $"documentos-electronicos/{carpeta}/{nombreArchivo}";

        using var contenidoStream = new MemoryStream(contenido);
        var solicitud = new PutObjectRequest
        {
            BucketName = BucketName,
            Key = clave,
            InputStream = contenidoStream,
            AutoCloseStream = false
        };

        try
        {
            await s3Cliente.PutObjectAsync(solicitud, cancellationToken);
        }
        catch (Exception ex)
        {
            // Este método devuelve string, no ResultadoOperacion — no hay dónde envolver el error acá sin
            // cambiar el contrato del puerto. Se loguea con el detalle real (credenciales AWS/permisos de
            // bucket/red de salida bloqueada son la diferencia más probable entre el entorno de desarrollo y
            // uno nuevo) y se re-lanza tal cual para que el catch general de EnviarDocumentoElectronicoASunatCasoDeUso
            // lo convierta en una respuesta con envelope.
            logger.LogError(ex, "AlmacenamientoS3 — falló PutObject. bucket={Bucket}, clave={Clave}.", BucketName, clave);
            throw;
        }

        return clave;
    }

    public string GenerarUrlDescarga(string ruta, string nombreArchivo, TimeSpan vigencia)
    {
        var solicitud = new GetPreSignedUrlRequest
        {
            BucketName = BucketName,
            Key = ruta,
            Expires = DateTime.UtcNow.Add(vigencia),
            Verb = HttpVerb.GET,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = $"attachment; filename=\"{nombreArchivo}\""
            }
        };

        return s3Cliente.GetPreSignedURL(solicitud);
    }
}
