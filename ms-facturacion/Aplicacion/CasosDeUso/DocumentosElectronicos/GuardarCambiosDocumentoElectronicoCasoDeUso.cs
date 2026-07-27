using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class GuardarCambiosDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<DocumentoElectronicoCambiosGuardados>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico,
        IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas, IReadOnlyList<CuotaDocumentoElectronico> cuotas,
        CancellationToken cancellationToken) =>
        repositorio.GuardarCambiosAsync(usuarioEjecutor, idInquilino, idDocumentoElectronico, lineas, cuotas, cancellationToken);
}
