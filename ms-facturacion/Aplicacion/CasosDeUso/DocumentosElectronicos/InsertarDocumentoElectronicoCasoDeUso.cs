using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class InsertarDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    // FechaEmision/HoraEmision del borrador son solo un valor inicial de inserción: ms-facturacion las fija
    // con su propio reloj, no confía en lo que mande el llamador. La emisión real se recalcula igual al
    // confirmar con SUNAT (ver EnviarDocumentoElectronicoASunatCasoDeUso).
    public Task<ResultadoOperacion<DocumentoElectronicoCreado>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string idExterno, string? numeroReferencia,
        int idTipoDocumentoMaestro,
        int idMonedaMaestro, decimal? tipoCambio, int idTipoOperacionMaestro, int? idFormaPago, ClienteDatosEntrada cliente,
        DocumentoAfectadoEntrada? documentoAfectado, IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas,
        IReadOnlyList<CuotaDocumentoElectronicoEntrada> cuotas, IReadOnlyList<CampoExtraEntrada> camposExtra,
        CancellationToken cancellationToken)
    {
        var ahora = RelojPeru.Ahora();
        return repositorio.InsertarAsync(
            usuarioEjecutor, idInquilino, idEmpresa, idExterno, numeroReferencia, idTipoDocumentoMaestro,
            DateOnly.FromDateTime(ahora), TimeOnly.FromDateTime(ahora), idMonedaMaestro, tipoCambio, idTipoOperacionMaestro, idFormaPago, cliente,
            documentoAfectado, lineas, cuotas, camposExtra, cancellationToken);
    }
}
