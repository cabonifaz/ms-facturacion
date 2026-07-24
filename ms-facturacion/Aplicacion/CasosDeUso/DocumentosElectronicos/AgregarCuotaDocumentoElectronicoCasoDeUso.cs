using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class AgregarCuotaDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<CuotaDocumentoElectronico>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico,
        DateOnly fechaVencimiento, decimal monto, CancellationToken cancellationToken) =>
        repositorio.AgregarCuotaAsync(usuarioEjecutor, idInquilino, idDocumentoElectronico, fechaVencimiento, monto, cancellationToken);
}
