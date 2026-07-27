using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.Inquilinos;

public sealed class ActualizarInquilinoCasoDeUso(IInquilinoRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, string codigo, string nombre, bool activo, CancellationToken cancellationToken) =>
        repositorio.ActualizarAsync(usuarioEjecutor, idInquilino, codigo, nombre, activo, cancellationToken);
}
