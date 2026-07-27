using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.Clientes;

public sealed class ListarClientesCasoDeUso(IClienteRepositorio repositorio)
{
    public Task<ResultadoOperacion<ResultadoPaginado<ClienteResumen>>> EjecutarAsync(
        int idInquilino, string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(idInquilino, busqueda, numeroPagina, tamanoPagina, cancellationToken);
}
