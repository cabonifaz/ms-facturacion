using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.ConfiguracionesFacturacionEmpresa;

public sealed class ObtenerConfiguracionFacturacionEmpresaCasoDeUso(IConfiguracionFacturacionEmpresaRepositorio repositorio)
{
    public Task<ResultadoOperacion<Dominio.ConfiguracionFacturacionEmpresa>> EjecutarAsync(
        int idInquilino, int idConfiguracionFacturacionEmpresa, CancellationToken cancellationToken) =>
        repositorio.ObtenerAsync(idInquilino, idConfiguracionFacturacionEmpresa, cancellationToken);
}
