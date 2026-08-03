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
        string usuarioEjecutor, int idInquilino, int idEmpresa, string idExterno,
        int idTipoDocumentoMaestro,
        int idMonedaMaestro, int idTipoOperacionMaestro, int idFormaPago, ClienteDatosEntrada cliente,
        DocumentoAfectadoEntrada? documentoAfectado, IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas,
        IReadOnlyList<CuotaDocumentoElectronico> cuotas, CancellationToken cancellationToken)
    {
        var ahora = DateTime.Now;
        return repositorio.InsertarAsync(
            usuarioEjecutor, idInquilino, idEmpresa, idExterno, idTipoDocumentoMaestro,
            DateOnly.FromDateTime(ahora), TimeOnly.FromDateTime(ahora), idMonedaMaestro, idTipoOperacionMaestro, idFormaPago, cliente,
            documentoAfectado, lineas, cuotas, cancellationToken);
    }
}
