using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.SeriesDocumento;

public sealed class EliminarSerieDocumentoCasoDeUso(ISerieDocumentoRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idSerieDocumento, CancellationToken cancellationToken) =>
        repositorio.EliminarAsync(usuarioEjecutor, idInquilino, idSerieDocumento, cancellationToken);
}
