using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class ListarDocumentosElectronicosCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<ResultadoPaginado<DocumentoElectronicoResumen>>> EjecutarAsync(
        int idInquilino, int idEmpresa, string? estadoCodigo, string? busqueda, DateOnly? fechaDesde, DateOnly? fechaHasta,
        int numeroPagina, int tamanoPagina, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(idInquilino, idEmpresa, estadoCodigo, busqueda, fechaDesde, fechaHasta, numeroPagina, tamanoPagina, cancellationToken);
}
