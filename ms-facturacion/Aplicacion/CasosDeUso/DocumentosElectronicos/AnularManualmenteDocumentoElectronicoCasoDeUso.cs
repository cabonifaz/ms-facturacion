using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class AnularManualmenteDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<EstadoDocumentoElectronicoActualizado>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, string motivo, DateTime fechaAnulacion,
        CancellationToken cancellationToken) =>
        repositorio.AnularManualmenteAsync(usuarioEjecutor, idInquilino, idDocumentoElectronico, motivo, fechaAnulacion, cancellationToken);
}
