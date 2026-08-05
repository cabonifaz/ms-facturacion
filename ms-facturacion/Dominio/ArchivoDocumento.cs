namespace ms_facturacion.Dominio;

/// Datos necesarios para registrar un archivo generado (XML/ZIP/CDR/PDF) — solo escritura en este pase.
public sealed record ArchivoDocumento(
    int? IdDocumentoElectronico, int? IdLoteDocumento, string TipoArchivoCodigo, string NombreArchivo,
    string RutaAlmacenamiento, string TipoContenido, string HashSha256, long TamanoBytes);

/// Lo mínimo necesario para armar una URL presignada de descarga (Xml o Pdf) — ver
/// SP_ArchivoDocumento_ObtenerXmlYPdf.
public sealed record ArchivoDescarga(string NombreArchivo, string RutaAlmacenamiento, string TipoContenido);
