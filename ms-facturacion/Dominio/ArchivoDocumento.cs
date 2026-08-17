namespace ms_facturacion.Dominio;

/// Datos necesarios para registrar un archivo generado (XML/ZIP/CDR/PDF) — solo escritura en este pase.
/// IdTransmisionSunat vincula el archivo a la transmisión que lo produjo (TipoArchivoCodigo indica su rol
/// dentro de esa transmisión) — reemplaza los 4 slots fijos que tenía TRANSMISIONES_SUNAT
/// (IdArchivoSolicitud/IdArchivoRespuesta/IdArchivoXml/IdArchivoPdf). Puede ser null cuando el archivo no
/// está atado a ninguna transmisión real (hoy no aplica a ningún caso de uso, pero la columna es NULL en
/// la tabla).
public sealed record ArchivoDocumento(
    int? IdDocumentoElectronico, int? IdLoteDocumento, int? IdTransmisionSunat, string TipoArchivoCodigo,
    string NombreArchivo, string RutaAlmacenamiento, string TipoContenido, string HashSha256, long TamanoBytes);

/// Lo mínimo necesario para armar una URL presignada de descarga (Xml o Pdf) — ver
/// SP_ArchivoDocumento_ObtenerXmlYPdf.
public sealed record ArchivoDescarga(string NombreArchivo, string RutaAlmacenamiento, string TipoContenido);
