namespace ms_facturacion.Aplicacion.Puertos;

/// Persiste bytes de archivo (XML/ZIP/CDR) y devuelve la ruta donde quedó guardado — hoy disco local,
/// mismo espíritu que CERTIFICADOS.RutaAlmacenamiento; el resto del sistema solo conoce esta interfaz.
public interface IAlmacenamientoArchivosServicio
{
    Task<string> GuardarAsync(string nombreArchivo, byte[] contenido, CancellationToken cancellationToken);
}
