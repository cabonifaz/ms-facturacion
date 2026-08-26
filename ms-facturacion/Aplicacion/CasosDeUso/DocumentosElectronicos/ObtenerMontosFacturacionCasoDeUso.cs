using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Dashboard de Facturación Analítica (Gerente) en maximlian3_backend — ver SP_Facturacion_ObtenerMontosFacturacion.
public sealed class ObtenerMontosFacturacionCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<MontosFacturacion>> EjecutarAsync(
        int idInquilino, int idEmpresa, DateOnly? fechaDesde, DateOnly? fechaHasta, CancellationToken cancellationToken) =>
        repositorio.ObtenerMontosFacturacionAsync(idInquilino, idEmpresa, fechaDesde, fechaHasta, cancellationToken);
}
