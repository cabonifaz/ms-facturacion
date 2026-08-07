using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class GuardarCambiosDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<DocumentoElectronicoCambiosGuardados>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idFormaPago, string? numeroReferencia,
        int idMonedaMaestro, decimal? tipoCambio, int idTipoOperacionMaestro,
        IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas, IReadOnlyList<CuotaDocumentoElectronico> cuotas,
        IReadOnlyList<CampoExtraEntrada> camposExtra, CancellationToken cancellationToken) =>
        repositorio.GuardarCambiosAsync(
            usuarioEjecutor, idInquilino, idDocumentoElectronico, idFormaPago, numeroReferencia,
            idMonedaMaestro, tipoCambio, idTipoOperacionMaestro, lineas, cuotas, camposExtra, cancellationToken);
}
