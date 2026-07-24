using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class ActualizarCuotaDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<CuotaDocumentoElectronico>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idCuotaDocumentoElectronico,
        DateOnly fechaVencimiento, decimal monto, CancellationToken cancellationToken) =>
        repositorio.ActualizarCuotaAsync(
            usuarioEjecutor, idInquilino, idDocumentoElectronico, idCuotaDocumentoElectronico, fechaVencimiento, monto, cancellationToken);
}
