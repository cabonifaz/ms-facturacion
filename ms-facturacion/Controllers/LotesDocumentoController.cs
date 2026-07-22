using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;
using ms_facturacion.Dominio;

namespace ms_facturacion.Controllers;

public sealed record ItemBajaPeticion(int IdDocumentoElectronico, string MotivoDescripcion);

public sealed record ComunicacionBajaPeticion(
    int IdInquilino, int IdEmpresa, string AmbienteCodigo, DateOnly FechaReferencia, IReadOnlyList<ItemBajaPeticion> Items);

[ApiController]
[Route("api/v1/lotes-documento")]
public sealed class LotesDocumentoController(
    EnviarComunicacionBajaASunatCasoDeUso enviarBajaCasoDeUso,
    ConsultarTicketComunicacionBajaCasoDeUso consultarTicketCasoDeUso) : ControllerBase
{
    // Comunicación de Baja: crea el lote y lo envía a SUNAT en el mismo paso — el resultado esperable
    // de éxito es un ticket (sendSummary nunca resuelve en la misma llamada), no un veredicto final.
    [HttpPost("comunicacion-baja")]
    public async Task<IActionResult> ComunicacionBaja(ComunicacionBajaPeticion peticion, CancellationToken cancellationToken)
    {
        var items = peticion.Items
            .Select(item => new ItemBajaEntrada(item.IdDocumentoElectronico, item.MotivoDescripcion))
            .ToList();

        var resultado = await enviarBajaCasoDeUso.EjecutarAsync(
            peticion.IdInquilino, peticion.IdEmpresa, peticion.FechaReferencia, items, peticion.AmbienteCodigo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    // Dispara un getStatus. Si el resultado es TicketPendiente, el llamador debe reintentar más tarde
    // (no hay poller/BackgroundService todavía — fuera de alcance de este pase).
    [HttpPost("{idLoteDocumento:int}/consultar-ticket")]
    public async Task<IActionResult> ConsultarTicket(
        [FromQuery] int idInquilino, int idLoteDocumento, [FromQuery] string ambienteCodigo, CancellationToken cancellationToken)
    {
        var resultado = await consultarTicketCasoDeUso.EjecutarAsync(idInquilino, idLoteDocumento, ambienteCodigo, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    private IActionResult ResponderSegunEnvelope<T>(ResultadoOperacion<T> resultado) => resultado.IdTipoMensaje switch
    {
        TipoMensaje.Exito => Ok(new { resultado.Mensaje, Datos = resultado.Datos }),
        TipoMensaje.ReglaDeNegocio => BadRequest(new { resultado.Mensaje }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { resultado.Mensaje })
    };
}
