using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.Inquilinos;

public sealed class ListarInquilinosCasoDeUso(IInquilinoRepositorio repositorio)
{
    public Task<ResultadoOperacion<ResultadoPaginado<InquilinoResumen>>> EjecutarAsync(
        string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(busqueda, numeroPagina, tamanoPagina, cancellationToken);
}
