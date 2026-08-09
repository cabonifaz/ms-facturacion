using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Expone el snapshot de cliente de un documento ya emitido — usado para prellenar el receptor de una
/// Nota de Crédito/Débito con el mismo cliente del documento afectado, sin volver a tipearlo.
public sealed class ObtenerClienteDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<ClienteDatosEntrada>> EjecutarAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken) =>
        repositorio.ObtenerClienteAsync(idInquilino, idDocumentoElectronico, cancellationToken);
}
