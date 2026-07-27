using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.Clientes;

public sealed class ObtenerClienteCasoDeUso(IClienteRepositorio repositorio)
{
    public Task<ResultadoOperacion<Cliente>> EjecutarAsync(
        int idInquilino, int idCliente, CancellationToken cancellationToken) =>
        repositorio.ObtenerAsync(idInquilino, idCliente, cancellationToken);
}
