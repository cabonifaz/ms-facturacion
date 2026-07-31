using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class InsertarDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<DocumentoElectronicoCreado>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string idExterno,
        int idTipoDocumentoMaestro, int idSerieDocumento, DateOnly fechaEmision, TimeOnly horaEmision,
        string monedaCodigo, string tipoOperacionCodigo, string formaPagoCodigo, ClienteDatosEntrada cliente,
        DocumentoAfectadoEntrada? documentoAfectado, IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas,
        IReadOnlyList<CuotaDocumentoElectronico> cuotas, CancellationToken cancellationToken) =>
        repositorio.InsertarAsync(
            usuarioEjecutor, idInquilino, idEmpresa, idExterno, idTipoDocumentoMaestro, idSerieDocumento,
            fechaEmision, horaEmision, monedaCodigo, tipoOperacionCodigo, formaPagoCodigo, cliente,
            documentoAfectado, lineas, cuotas, cancellationToken);
}
