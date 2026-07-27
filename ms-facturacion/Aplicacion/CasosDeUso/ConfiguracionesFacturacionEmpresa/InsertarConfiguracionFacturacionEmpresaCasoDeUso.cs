using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.ConfiguracionesFacturacionEmpresa;

public sealed class InsertarConfiguracionFacturacionEmpresaCasoDeUso(IConfiguracionFacturacionEmpresaRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string ambienteCodigo, string tipoProveedorCodigo,
        string? nombreProveedor, int idCertificado, string? urlEnvioFacturaBoletaNota, string? urlEnvioRetencionPercepcion,
        string? urlEnvioGuiaRemision, string? urlConsultaEstadoCdr, string? urlConsultaValidez, bool activo,
        CancellationToken cancellationToken) =>
        repositorio.InsertarAsync(
            usuarioEjecutor, idInquilino, idEmpresa, ambienteCodigo, tipoProveedorCodigo, nombreProveedor, idCertificado,
            urlEnvioFacturaBoletaNota, urlEnvioRetencionPercepcion, urlEnvioGuiaRemision, urlConsultaEstadoCdr,
            urlConsultaValidez, activo, cancellationToken);
}
