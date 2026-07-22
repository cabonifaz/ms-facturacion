using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.Certificados;

public sealed class InsertarCertificadoCasoDeUso(ICertificadoRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string rutaAlmacenamiento, string sujeto, string emisor,
        string numeroSerie, string huellaDigital, DateOnly validoDesde, DateOnly validoHasta, bool activo,
        CancellationToken cancellationToken) =>
        repositorio.InsertarAsync(
            usuarioEjecutor, idInquilino, idEmpresa, rutaAlmacenamiento, sujeto, emisor,
            numeroSerie, huellaDigital, validoDesde, validoHasta, activo, cancellationToken);
}
