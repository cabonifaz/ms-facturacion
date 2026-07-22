using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.Credenciales;

public sealed class EliminarCredencialCasoDeUso(ICredencialInquilinoRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idCredencialInquilino, CancellationToken cancellationToken) =>
        repositorio.EliminarAsync(usuarioEjecutor, idInquilino, idCredencialInquilino, cancellationToken);
}
