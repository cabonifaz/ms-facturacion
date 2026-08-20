using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;

/// Previsualización de EnviarResumenBajaBoletaASunatCasoDeUso — ver SP_LoteResumenBajaBoleta_PrevisualizarBaja.
public sealed class PrevisualizarResumenBajaBoletaCasoDeUso(ILoteDocumentoRepositorio loteRepositorio)
{
    public Task<ResultadoOperacion<IReadOnlyList<DocumentoBajaPreview>>> EjecutarAsync(
        int idInquilino, int idEmpresa, IReadOnlyList<int> idsDocumentoElectronico, CancellationToken cancellationToken)
    {
        var fechaGeneracion = DateOnly.FromDateTime(RelojPeru.Ahora());
        return loteRepositorio.PrevisualizarResumenBajaBoletaAsync(idInquilino, idEmpresa, fechaGeneracion, idsDocumentoElectronico, cancellationToken);
    }
}
