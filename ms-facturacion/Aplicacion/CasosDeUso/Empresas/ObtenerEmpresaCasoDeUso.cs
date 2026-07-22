using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.Empresas;

public sealed class ObtenerEmpresaCasoDeUso(IEmpresaRepositorio repositorio)
{
    public Task<ResultadoOperacion<Empresa>> EjecutarAsync(
        int idInquilino, int idEmpresa, CancellationToken cancellationToken) =>
        repositorio.ObtenerAsync(idInquilino, idEmpresa, cancellationToken);
}
