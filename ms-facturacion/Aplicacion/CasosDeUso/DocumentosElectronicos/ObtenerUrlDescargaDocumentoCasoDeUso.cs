using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Resuelve la ruta real en S3 (vía SP_ArchivoDocumento_ObtenerXmlYPdf, último intento de transmisión) y
/// arma una URL presignada de descarga directa — el archivo nunca pasa por este servicio, solo la URL.
public sealed class ObtenerUrlDescargaDocumentoCasoDeUso(
    IArchivoDocumentoRepositorio archivoRepositorio, IAlmacenamientoArchivosServicio almacenamiento)
{
    private static readonly TimeSpan VigenciaUrl = TimeSpan.FromMinutes(5);

    public async Task<ResultadoOperacion<string>> EjecutarAsync(
        int idInquilino, int idDocumentoElectronico, string tipoArchivoCodigo, CancellationToken cancellationToken)
    {
        var archivo = await archivoRepositorio.ObtenerXmlOPdfAsync(idInquilino, idDocumentoElectronico, tipoArchivoCodigo, cancellationToken);
        if (archivo.IdTipoMensaje != TipoMensaje.Exito || archivo.Datos is null)
        {
            return new ResultadoOperacion<string>(archivo.IdTipoMensaje, archivo.Mensaje, default);
        }

        var url = almacenamiento.GenerarUrlDescarga(archivo.Datos.RutaAlmacenamiento, archivo.Datos.NombreArchivo, VigenciaUrl);

        return ResultadoOperacion<string>.DeExito("URL de descarga generada.", url);
    }
}
