using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Uso exclusivo del Worker (Módulo 4) tras recibir la respuesta de SUNAT — no expuesto como Actualizar genérico.
public sealed class ActualizarEstadoSunatDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<EstadoDocumentoElectronicoActualizado>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, EstadoMaestroCodigo estadoCodigo, string? sunatHash,
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, string? sunatTicket, CancellationToken cancellationToken) =>
        repositorio.ActualizarEstadoSunatAsync(
            usuarioEjecutor, idInquilino, idDocumentoElectronico, estadoCodigo, sunatHash,
            sunatCodigoRespuesta, sunatDescripcionRespuesta, sunatTicket, cancellationToken);
}
