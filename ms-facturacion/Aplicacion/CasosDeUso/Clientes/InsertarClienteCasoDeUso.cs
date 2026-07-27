using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.Clientes;

public sealed class InsertarClienteCasoDeUso(IClienteRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idTipoDocumento, string numeroDocumento, string nombre,
        string? correo, string? direccion, int paisCodigo, CancellationToken cancellationToken) =>
        repositorio.InsertarAsync(
            usuarioEjecutor, idInquilino, idTipoDocumento, numeroDocumento, nombre, correo, direccion, paisCodigo, cancellationToken);
}
