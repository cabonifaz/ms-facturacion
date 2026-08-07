namespace ms_facturacion.Aplicacion.Puertos;

/// Persiste bytes de archivo (XML/ZIP/CDR) y devuelve la clave donde quedó guardado — S3
/// (AlmacenamientoArchivosS3Servicio), mismo espíritu que CERTIFICADOS.RutaAlmacenamiento; el resto del
/// sistema solo conoce esta interfaz.
public interface IAlmacenamientoArchivosServicio
{
    /// carpeta = ruta relativa dentro de "documentos-electronicos/" (idInquilino/idEmpresa/año/mes/serie-correlativo
    /// o .../baja-{nombreLote}) — cada llamador arma la suya, el adaptador solo concatena.
    Task<string> GuardarAsync(string carpeta, string nombreArchivo, byte[] contenido, CancellationToken cancellationToken);

    /// ruta = la clave completa de S3 devuelta por GuardarAsync (ya guardada en ARCHIVOS_DOCUMENTO.
    /// RutaAlmacenamiento) — genera una URL temporal para descargar ese objeto directo desde S3, sin pasar
    /// el archivo por este servicio. nombreArchivo fuerza el nombre de descarga vía Content-Disposition.
    string GenerarUrlDescarga(string ruta, string nombreArchivo, TimeSpan vigencia);
}
