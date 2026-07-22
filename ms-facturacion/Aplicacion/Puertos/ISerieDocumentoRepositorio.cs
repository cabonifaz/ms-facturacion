using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface ISerieDocumentoRepositorio
{
    Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string tipoDocumentoCodigo, string serie,
        int numeroActual, bool activo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<SerieDocumento>> ObtenerAsync(
        int idInquilino, int idSerieDocumento, CancellationToken cancellationToken);

    Task<ResultadoOperacion<ResultadoPaginado<SerieDocumentoResumen>>> ListarAsync(
        int idInquilino, int idEmpresa, string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idSerieDocumento, string tipoDocumentoCodigo, string serie,
        int numeroActual, bool activo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idSerieDocumento, CancellationToken cancellationToken);
}
