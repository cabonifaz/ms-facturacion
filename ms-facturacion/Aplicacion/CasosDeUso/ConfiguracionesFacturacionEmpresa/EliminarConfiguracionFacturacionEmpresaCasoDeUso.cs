using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.ConfiguracionesFacturacionEmpresa;

public sealed class EliminarConfiguracionFacturacionEmpresaCasoDeUso(IConfiguracionFacturacionEmpresaRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idConfiguracionFacturacionEmpresa, CancellationToken cancellationToken) =>
        repositorio.EliminarAsync(usuarioEjecutor, idInquilino, idConfiguracionFacturacionEmpresa, cancellationToken);
}
