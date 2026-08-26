using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Dashboard de Facturación Analítica (Gerente) en maximlian3_backend — ver SP_Facturacion_ObtenerDesgloseEstado.
public sealed class ObtenerDesgloseEstadoFacturacionCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<IReadOnlyList<DesgloseEstadoFacturacion>>> EjecutarAsync(
        int idInquilino, int idEmpresa, DateOnly? fechaDesde, DateOnly? fechaHasta, int? idTipoDocumentoMaestro,
        CancellationToken cancellationToken) =>
        repositorio.ObtenerDesgloseEstadoFacturacionAsync(idInquilino, idEmpresa, fechaDesde, fechaHasta, idTipoDocumentoMaestro, cancellationToken);
}
