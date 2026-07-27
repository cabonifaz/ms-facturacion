using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.Credenciales;

public sealed class ListarCredencialesCasoDeUso(ICredencialInquilinoRepositorio repositorio)
{
    public Task<ResultadoOperacion<ResultadoPaginado<CredencialInquilinoResumen>>> EjecutarAsync(
        int idInquilino, int idEmpresa, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(idInquilino, idEmpresa, numeroPagina, tamanoPagina, cancellationToken);
}
