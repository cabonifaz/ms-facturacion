using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.SeriesDocumento;

public sealed class ObtenerSerieDocumentoCasoDeUso(ISerieDocumentoRepositorio repositorio)
{
    public Task<ResultadoOperacion<SerieDocumento>> EjecutarAsync(
        int idInquilino, int idSerieDocumento, CancellationToken cancellationToken) =>
        repositorio.ObtenerAsync(idInquilino, idSerieDocumento, cancellationToken);
}
