using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface IClienteRepositorio
{
    Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idTipoDocumento, string numeroDocumento, string nombre,
        string? correo, string? direccion, int paisCodigo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<Cliente>> ObtenerAsync(
        int idInquilino, int idCliente, CancellationToken cancellationToken);

    Task<ResultadoOperacion<ResultadoPaginado<ClienteResumen>>> ListarAsync(
        int idInquilino, string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idCliente, int idTipoDocumento, string numeroDocumento,
        string nombre, string? correo, string? direccion, int paisCodigo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idCliente, CancellationToken cancellationToken);
}
