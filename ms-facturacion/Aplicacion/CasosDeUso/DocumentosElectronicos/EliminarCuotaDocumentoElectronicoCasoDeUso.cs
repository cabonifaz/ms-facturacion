using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class EliminarCuotaDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<bool>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idCuotaDocumentoElectronico,
        CancellationToken cancellationToken) =>
        repositorio.EliminarCuotaAsync(usuarioEjecutor, idInquilino, idDocumentoElectronico, idCuotaDocumentoElectronico, cancellationToken);
}
