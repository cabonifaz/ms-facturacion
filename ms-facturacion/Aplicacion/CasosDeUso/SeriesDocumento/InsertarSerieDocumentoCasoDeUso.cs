using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.SeriesDocumento;

public sealed class InsertarSerieDocumentoCasoDeUso(ISerieDocumentoRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string tipoDocumentoCodigo, string serie,
        int numeroActual, bool activo, CancellationToken cancellationToken) =>
        repositorio.InsertarAsync(usuarioEjecutor, idInquilino, idEmpresa, tipoDocumentoCodigo, serie, numeroActual, activo, cancellationToken);
}
