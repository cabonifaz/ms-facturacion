using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Expone cliente + listado de productos de un documento ya emitido — usado para prellenar el receptor y
/// listar los productos del documento afectado al armar una Nota de Crédito/Débito.
public sealed class ObtenerParaNotaCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<DatosParaNota>> EjecutarAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken) =>
        repositorio.ObtenerParaNotaAsync(idInquilino, idDocumentoElectronico, cancellationToken);
}
