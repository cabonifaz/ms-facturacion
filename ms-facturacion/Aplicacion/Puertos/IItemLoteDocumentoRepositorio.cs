using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface IItemLoteDocumentoRepositorio
{
    Task<ResultadoOperacion<int>> ActualizarEstadoSunatTodosAsync(
        string usuarioEjecutor, int idInquilino, int idLoteDocumento, EstadoMaestroCodigo estadoItemCodigo,
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, CancellationToken cancellationToken);
}
