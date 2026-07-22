using ms_facturacion.Aplicacion.Comun;

namespace ms_facturacion.Aplicacion.Puertos;

public interface IItemLoteDocumentoRepositorio
{
    Task<ResultadoOperacion<int>> ActualizarEstadoSunatTodosAsync(
        string usuarioEjecutor, int idInquilino, int idLoteDocumento, string estadoItemCodigo,
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, CancellationToken cancellationToken);
}
