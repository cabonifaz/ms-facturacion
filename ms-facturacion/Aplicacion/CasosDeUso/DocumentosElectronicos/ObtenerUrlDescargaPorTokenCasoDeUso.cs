using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Mismo resultado que ObtenerUrlDescargaDocumentoCasoDeUso, pero de cara a la verificación pública:
/// SP_ArchivoDocumento_ObtenerXmlYPdfPorToken ya resuelve el documento por TokenPublico, no requiere un
/// paso previo de resolución.
public sealed class ObtenerUrlDescargaPorTokenCasoDeUso(
    IArchivoDocumentoRepositorio archivoRepositorio, IAlmacenamientoArchivosServicio almacenamiento)
{
    private static readonly TimeSpan VigenciaUrl = TimeSpan.FromMinutes(5);

    public async Task<ResultadoOperacion<string>> EjecutarAsync(
        string tokenPublico, string tipoArchivoCodigo, CancellationToken cancellationToken)
    {
        var archivo = await archivoRepositorio.ObtenerXmlOPdfPorTokenAsync(tokenPublico, tipoArchivoCodigo, cancellationToken);
        if (archivo.IdTipoMensaje != TipoMensaje.Exito || archivo.Datos is null)
        {
            return new ResultadoOperacion<string>(archivo.IdTipoMensaje, archivo.Mensaje, default);
        }

        var url = almacenamiento.GenerarUrlDescarga(archivo.Datos.RutaAlmacenamiento, archivo.Datos.NombreArchivo, VigenciaUrl);

        return ResultadoOperacion<string>.DeExito("URL de descarga generada.", url);
    }
}
