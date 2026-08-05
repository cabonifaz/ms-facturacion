using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class ListarDocumentosElectronicosParaPedidoFacturaCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<ResultadoPaginado<FacturaResumenPedidoFactura>>> EjecutarAsync(
        int idInquilino, int idEmpresa, string? estadoCodigo, int? idFormaPago, DateOnly? fechaDesde, DateOnly? fechaHasta,
        string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken) =>
        repositorio.ListarParaPedidoFacturaAsync(
            idInquilino, idEmpresa, estadoCodigo, idFormaPago, fechaDesde, fechaHasta, busqueda, numeroPagina, tamanoPagina, cancellationToken);
}
