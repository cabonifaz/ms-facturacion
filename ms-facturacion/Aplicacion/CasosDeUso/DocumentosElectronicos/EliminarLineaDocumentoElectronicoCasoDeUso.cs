using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class EliminarLineaDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<bool>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idLineaDocumentoElectronico,
        CancellationToken cancellationToken) =>
        repositorio.EliminarLineaAsync(usuarioEjecutor, idInquilino, idDocumentoElectronico, idLineaDocumentoElectronico, cancellationToken);
}
