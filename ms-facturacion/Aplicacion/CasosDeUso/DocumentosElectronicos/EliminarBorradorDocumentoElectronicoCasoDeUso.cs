using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class EliminarBorradorDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<bool>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken) =>
        repositorio.EliminarBorradorAsync(usuarioEjecutor, idInquilino, idDocumentoElectronico, cancellationToken);
}
