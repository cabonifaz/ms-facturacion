using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface IEmpresaRepositorio
{
    Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, string ruc, string razonSocial, string? nombreComercial,
        string direccion, string ubigeo, string departamento, string provincia, string distrito,
        string paisCodigo, bool activo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<Empresa>> ObtenerAsync(
        int idInquilino, int idEmpresa, CancellationToken cancellationToken);

    Task<ResultadoOperacion<ResultadoPaginado<EmpresaResumen>>> ListarAsync(
        int idInquilino, string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string ruc, string razonSocial, string? nombreComercial,
        string direccion, string ubigeo, string departamento, string provincia, string distrito,
        string paisCodigo, bool activo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, CancellationToken cancellationToken);
}
