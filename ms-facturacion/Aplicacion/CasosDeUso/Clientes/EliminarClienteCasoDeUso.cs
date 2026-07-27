using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.Clientes;

public sealed class EliminarClienteCasoDeUso(IClienteRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idCliente, CancellationToken cancellationToken) =>
        repositorio.EliminarAsync(usuarioEjecutor, idInquilino, idCliente, cancellationToken);
}
