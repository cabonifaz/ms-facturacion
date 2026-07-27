using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.Clientes;

public sealed class ActualizarClienteCasoDeUso(IClienteRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idCliente, int idTipoDocumento, string numeroDocumento,
        string nombre, string? correo, string? direccion, int paisCodigo, CancellationToken cancellationToken) =>
        repositorio.ActualizarAsync(
            usuarioEjecutor, idInquilino, idCliente, idTipoDocumento, numeroDocumento, nombre, correo, direccion,
            paisCodigo, cancellationToken);
}
