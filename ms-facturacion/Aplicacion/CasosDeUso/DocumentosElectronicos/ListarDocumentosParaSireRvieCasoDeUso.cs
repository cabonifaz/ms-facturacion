using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class ListarDocumentosParaSireRvieCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<IReadOnlyList<DocumentoSireRvie>>> EjecutarAsync(
        int idInquilino, int idEmpresa, DateOnly periodo, CancellationToken cancellationToken) =>
        repositorio.ListarParaSireRvieAsync(idInquilino, idEmpresa, periodo, cancellationToken);
}
