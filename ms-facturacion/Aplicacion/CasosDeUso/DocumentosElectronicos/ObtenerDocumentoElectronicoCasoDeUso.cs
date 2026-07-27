using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class ObtenerDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<DocumentoElectronicoDetalle>> EjecutarAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken) =>
        repositorio.ObtenerAsync(idInquilino, idDocumentoElectronico, cancellationToken);
}
