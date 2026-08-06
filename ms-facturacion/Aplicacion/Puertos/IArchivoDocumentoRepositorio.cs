using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface IArchivoDocumentoRepositorio
{
    Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, ArchivoDocumento archivo, CancellationToken cancellationToken);

    /// tipoArchivoCodigo: "Xml" o "Pdf" — resuelve contra el último intento de transmisión del documento
    /// (TRANSMISIONES_SUNAT.IdArchivoXml/IdArchivoPdf), ver SP_ArchivoDocumento_ObtenerXmlYPdf.
    Task<ResultadoOperacion<ArchivoDescarga>> ObtenerXmlOPdfAsync(
        int idInquilino, int idDocumentoElectronico, string tipoArchivoCodigo, CancellationToken cancellationToken);

    /// Camino de verificación pública: mismo resultado que ObtenerXmlOPdfAsync, pero resuelto por
    /// TokenPublico en vez de idInquilino/idDocumentoElectronico ya conocidos — ver
    /// SP_ArchivoDocumento_ObtenerXmlYPdfPorToken.
    Task<ResultadoOperacion<ArchivoDescarga>> ObtenerXmlOPdfPorTokenAsync(
        string tokenPublico, string tipoArchivoCodigo, CancellationToken cancellationToken);
}
