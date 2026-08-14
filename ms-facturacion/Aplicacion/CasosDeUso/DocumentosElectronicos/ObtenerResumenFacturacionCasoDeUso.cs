using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Dashboard de PedidoFactura en maximlian3_backend — ver SP_DocumentoElectronico_ObtenerResumenFacturacion.
public sealed class ObtenerResumenFacturacionCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<ResumenFacturacion>> EjecutarAsync(
        int idInquilino, int idEmpresa, DateOnly? fechaDesde, DateOnly? fechaHasta, CancellationToken cancellationToken) =>
        repositorio.ObtenerResumenFacturacionAsync(idInquilino, idEmpresa, fechaDesde, fechaHasta, cancellationToken);
}
