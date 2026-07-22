using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.Inquilinos;

public sealed class ObtenerInquilinoCasoDeUso(IInquilinoRepositorio repositorio)
{
    public Task<ResultadoOperacion<Inquilino>> EjecutarAsync(int idInquilino, CancellationToken cancellationToken) =>
        repositorio.ObtenerAsync(idInquilino, cancellationToken);
}
