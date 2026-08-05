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
}
