using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.ConfiguracionesFacturacionEmpresa;

public sealed class ListarConfiguracionesFacturacionEmpresaCasoDeUso(IConfiguracionFacturacionEmpresaRepositorio repositorio)
{
    public Task<ResultadoOperacion<ResultadoPaginado<ConfiguracionFacturacionEmpresaResumen>>> EjecutarAsync(
        int idInquilino, int idEmpresa, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(idInquilino, idEmpresa, numeroPagina, tamanoPagina, cancellationToken);
}
