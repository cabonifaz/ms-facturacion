using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.Certificados;

public sealed class ActualizarCertificadoCasoDeUso(ICertificadoRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idCertificado, string rutaAlmacenamiento, string sujeto, string emisor,
        string numeroSerie, string huellaDigital, DateOnly validoDesde, DateOnly validoHasta, bool activo,
        CancellationToken cancellationToken) =>
        repositorio.ActualizarAsync(
            usuarioEjecutor, idInquilino, idCertificado, rutaAlmacenamiento, sujeto, emisor,
            numeroSerie, huellaDigital, validoDesde, validoHasta, activo, cancellationToken);
}
