using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;

/// Previsualización de EnviarComunicacionBajaASunatCasoDeUso — ver SP_LoteDocumento_PrevisualizarBaja.
public sealed class PrevisualizarBajaCasoDeUso(ILoteDocumentoRepositorio loteRepositorio)
{
    public Task<ResultadoOperacion<IReadOnlyList<DocumentoBajaPreview>>> EjecutarAsync(
        int idInquilino, int idEmpresa, DateOnly fechaReferencia, IReadOnlyList<int> idsDocumentoElectronico,
        CancellationToken cancellationToken)
    {
        var fechaGeneracion = DateOnly.FromDateTime(RelojPeru.Ahora());
        return loteRepositorio.PrevisualizarBajaAsync(idInquilino, idEmpresa, fechaReferencia, fechaGeneracion, idsDocumentoElectronico, cancellationToken);
    }
}
