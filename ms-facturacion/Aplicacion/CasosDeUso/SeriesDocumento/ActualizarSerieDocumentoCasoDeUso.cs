using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.SeriesDocumento;

public sealed class ActualizarSerieDocumentoCasoDeUso(ISerieDocumentoRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idSerieDocumento, int idTipoDocumentoMaestro, string serie,
        int numeroActual, bool activo, CancellationToken cancellationToken) =>
        repositorio.ActualizarAsync(usuarioEjecutor, idInquilino, idSerieDocumento, idTipoDocumentoMaestro, serie, numeroActual, activo, cancellationToken);
}
