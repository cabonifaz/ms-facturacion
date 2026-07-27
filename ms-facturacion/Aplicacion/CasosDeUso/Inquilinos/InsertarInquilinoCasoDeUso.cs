using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.Inquilinos;

public sealed class InsertarInquilinoCasoDeUso(IInquilinoRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, string codigo, string nombre, bool activo, CancellationToken cancellationToken) =>
        repositorio.InsertarAsync(usuarioEjecutor, codigo, nombre, activo, cancellationToken);
}
