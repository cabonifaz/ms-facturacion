using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class ActualizarEstadoCuotaDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<CuotaDocumentoElectronico>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idCuotaDocumentoElectronico,
        EstadoCuotaCodigo estadoCuotaCodigo, DateTime? fechaPago, CancellationToken cancellationToken) =>
        repositorio.ActualizarEstadoCuotaAsync(
            usuarioEjecutor, idInquilino, idDocumentoElectronico, idCuotaDocumentoElectronico, estadoCuotaCodigo, fechaPago, cancellationToken);
}
