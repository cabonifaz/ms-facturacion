using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Dashboard de Facturación Analítica (Gerente) en maximlian3_backend — ver SP_Facturacion_ObtenerEvolucion.
public sealed class ObtenerEvolucionFacturacionCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<IReadOnlyList<EvolucionFacturacion>>> EjecutarAsync(
        int idInquilino, int idEmpresa, DateOnly? fechaDesde, DateOnly? fechaHasta, int granularidad,
        CancellationToken cancellationToken) =>
        repositorio.ObtenerEvolucionFacturacionAsync(idInquilino, idEmpresa, fechaDesde, fechaHasta, granularidad, cancellationToken);
}
