using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Previsualización de AnularManualmenteDocumentoElectronicoCasoDeUso — ver
/// SP_DocumentoElectronico_PrevisualizarAnulacionManual.
public sealed class PrevisualizarAnulacionManualCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<IReadOnlyList<DocumentoAnulacionManualPreview>>> EjecutarAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken) =>
        repositorio.PrevisualizarAnulacionManualAsync(idInquilino, idDocumentoElectronico, cancellationToken);
}
