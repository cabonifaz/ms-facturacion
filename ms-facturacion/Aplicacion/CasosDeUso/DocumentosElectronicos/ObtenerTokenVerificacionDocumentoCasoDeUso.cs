using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Expone el token público de un documento a un llamador autenticado (maximlian3_backend) para que arme el
/// link de verificación pública ({frontendBaseUrl}/{token}) — a diferencia de ObtenerAsync/SP_Obtener, que
/// deliberadamente no lo incluye en su result set.
public sealed class ObtenerTokenVerificacionDocumentoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<string>> EjecutarAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken) =>
        repositorio.ObtenerTokenPublicoAsync(idInquilino, idDocumentoElectronico, cancellationToken);
}
