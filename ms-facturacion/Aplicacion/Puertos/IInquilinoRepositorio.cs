using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface IInquilinoRepositorio
{
    Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, string codigo, string nombre, bool activo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<Inquilino>> ObtenerAsync(
        int idInquilino, CancellationToken cancellationToken);

    Task<ResultadoOperacion<ResultadoPaginado<InquilinoResumen>>> ListarAsync(
        string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, string codigo, string nombre, bool activo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, CancellationToken cancellationToken);
}
