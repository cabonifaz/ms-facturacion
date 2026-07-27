namespace ms_facturacion.Aplicacion.Puertos;

/// Persiste bytes de archivo (XML/ZIP/CDR) y devuelve la clave donde quedó guardado — S3
/// (AlmacenamientoArchivosS3Servicio), mismo espíritu que CERTIFICADOS.RutaAlmacenamiento; el resto del
/// sistema solo conoce esta interfaz.
public interface IAlmacenamientoArchivosServicio
{
    Task<string> GuardarAsync(string nombreArchivo, byte[] contenido, CancellationToken cancellationToken);
}
