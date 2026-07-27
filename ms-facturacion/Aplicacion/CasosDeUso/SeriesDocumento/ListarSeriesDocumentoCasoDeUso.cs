using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.SeriesDocumento;

public sealed class ListarSeriesDocumentoCasoDeUso(ISerieDocumentoRepositorio repositorio)
{
    public Task<ResultadoOperacion<ResultadoPaginado<SerieDocumentoResumen>>> EjecutarAsync(
        int idInquilino, int idEmpresa, string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(idInquilino, idEmpresa, busqueda, numeroPagina, tamanoPagina, cancellationToken);
}
