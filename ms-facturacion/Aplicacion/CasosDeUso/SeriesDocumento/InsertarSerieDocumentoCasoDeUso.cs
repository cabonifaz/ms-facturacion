using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.SeriesDocumento;

public sealed class InsertarSerieDocumentoCasoDeUso(ISerieDocumentoRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, int idTipoDocumentoMaestro, string serie,
        int numeroActual, bool activo, CancellationToken cancellationToken) =>
        repositorio.InsertarAsync(usuarioEjecutor, idInquilino, idEmpresa, idTipoDocumentoMaestro, serie, numeroActual, activo, cancellationToken);
}
