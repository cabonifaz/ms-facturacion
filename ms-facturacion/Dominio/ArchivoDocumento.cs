namespace ms_facturacion.Dominio;

/// Datos necesarios para registrar un archivo generado (XML/ZIP/CDR) — solo escritura en este pase.
public sealed record ArchivoDocumento(
    int? IdDocumentoElectronico, int? IdLoteDocumento, string TipoArchivoCodigo, string NombreArchivo,
    string RutaAlmacenamiento, string TipoContenido, string HashSha256, long TamanoBytes);
