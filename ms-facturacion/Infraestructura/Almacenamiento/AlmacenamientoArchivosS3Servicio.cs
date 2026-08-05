using Amazon.S3;
using Amazon.S3.Model;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Infraestructura.Almacenamiento;

/// Adaptador de infraestructura (driven) para IAlmacenamientoArchivosServicio — sube XML/ZIP/CDR a S3.
/// Mismo patrón de conexión que maximlian3_backend/SafetyReport.Handlers/S3UploadService.cs: IAmazonS3
/// inyectado (credenciales estáticas + región resueltas una sola vez en Program.cs), nombre de bucket
/// leído de configuración. El resto del sistema solo conoce IAlmacenamientoArchivosServicio.
public sealed class AlmacenamientoArchivosS3Servicio(IAmazonS3 s3Cliente, IConfiguration configuracion) : IAlmacenamientoArchivosServicio
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

        await s3Cliente.PutObjectAsync(solicitud, cancellationToken);

        return clave;
    }
}
