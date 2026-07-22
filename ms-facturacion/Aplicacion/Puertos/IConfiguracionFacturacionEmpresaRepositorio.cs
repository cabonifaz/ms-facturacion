using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface IConfiguracionFacturacionEmpresaRepositorio
{
    Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string ambienteCodigo, string tipoProveedorCodigo,
        string? nombreProveedor, int idCertificado, string? urlEnvioFacturaBoletaNota, string? urlEnvioRetencionPercepcion,
        string? urlEnvioGuiaRemision, string? urlConsultaEstadoCdr, string? urlConsultaValidez, bool activo,
        CancellationToken cancellationToken);

    Task<ResultadoOperacion<ConfiguracionFacturacionEmpresa>> ObtenerAsync(
        int idInquilino, int idConfiguracionFacturacionEmpresa, CancellationToken cancellationToken);

    Task<ResultadoOperacion<ConfiguracionFacturacionEmpresaPorAmbiente>> ObtenerPorEmpresaYAmbienteAsync(
        int idInquilino, int idEmpresa, string ambienteCodigo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<ResultadoPaginado<ConfiguracionFacturacionEmpresaResumen>>> ListarAsync(
        int idInquilino, int idEmpresa, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idConfiguracionFacturacionEmpresa, string ambienteCodigo,
        string tipoProveedorCodigo, string? nombreProveedor, int idCertificado, string? urlEnvioFacturaBoletaNota,
        string? urlEnvioRetencionPercepcion, string? urlEnvioGuiaRemision, string? urlConsultaEstadoCdr,
        string? urlConsultaValidez, bool activo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idConfiguracionFacturacionEmpresa, CancellationToken cancellationToken);
}
