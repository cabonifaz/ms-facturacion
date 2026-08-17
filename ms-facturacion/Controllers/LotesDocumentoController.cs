using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;
using ms_facturacion.Dominio;

namespace ms_facturacion.Controllers;

public sealed record ItemBajaPeticion(int IdDocumentoElectronico, string MotivoDescripcion);

public sealed record ComunicacionBajaPeticion(
    int IdInquilino, int IdEmpresa, DateOnly FechaReferencia, IReadOnlyList<ItemBajaPeticion> Items);

[ApiController]
[Route("api/v1/lotes-documento")]
public sealed class LotesDocumentoController(
    EnviarComunicacionBajaASunatCasoDeUso enviarBajaCasoDeUso,
    PrevisualizarBajaCasoDeUso previsualizarBajaCasoDeUso,
    ConsultarTicketComunicacionBajaCasoDeUso consultarTicketCasoDeUso,
    IHostEnvironment entorno) : ControllerBase
{
    // Comunicación de Baja: crea el lote y lo envía a SUNAT en el mismo paso — el resultado esperable
    // de éxito es un ticket (sendSummary nunca resuelve en la misma llamada), no un veredicto final.
    // AmbienteCodigo (Beta/Produccion) se deriva del entorno real del servidor, no de un valor mandado por
    // el llamador — así una request no puede hacer que esta instancia le pegue al SUNAT equivocado.
    [HttpPost("comunicacion-baja")]
    public async Task<IActionResult> ComunicacionBaja(ComunicacionBajaPeticion peticion, CancellationToken cancellationToken)
    {
        var items = peticion.Items
            .Select(item => new ItemBajaEntrada(item.IdDocumentoElectronico, item.MotivoDescripcion))
            .ToList();

        var ambienteCodigo = entorno.IsDevelopment() || entorno.IsStaging() ? "Beta" : "Produccion";
        var resultado = await enviarBajaCasoDeUso.EjecutarAsync(
            peticion.IdInquilino, peticion.IdEmpresa, peticion.FechaReferencia, items, ambienteCodigo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    // Previsualiza ComunicacionBaja sin ejecutar nada — mismas validaciones y, de poder enviarse, la lista
    // de documentos que se verían incluidos (los indicados + las Notas vigentes que se arrastrarían). Sin
    // MotivoDescripcion por ítem (a diferencia de ComunicacionBajaPeticion) — la previsualización nunca lo
    // lee (SP_LoteDocumento_PrevisualizarBaja no lo necesita para ninguna validación), así que el payload
    // que queda es solo escalares + una lista de ids, entra entero en query string — GET, no POST.
    [HttpGet("comunicacion-baja/preview")]
    public async Task<IActionResult> PrevisualizarComunicacionBaja(
        [FromQuery] int idInquilino, [FromQuery] int idEmpresa, [FromQuery] DateOnly fechaReferencia,
        [FromQuery] IReadOnlyList<int> idsDocumentoElectronico, CancellationToken cancellationToken)
    {
        var resultado = await previsualizarBajaCasoDeUso.EjecutarAsync(
            idInquilino, idEmpresa, fechaReferencia, idsDocumentoElectronico, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    // Dispara un getStatus. Si el resultado es TicketPendiente, el llamador debe reintentar más tarde
    // (no hay poller/BackgroundService todavía — fuera de alcance de este pase).
    [HttpPost("{idLoteDocumento:int}/consultar-ticket")]
    public async Task<IActionResult> ConsultarTicket(
        [FromQuery] int idInquilino, int idLoteDocumento, CancellationToken cancellationToken)
    {
        var ambienteCodigo = entorno.IsDevelopment() || entorno.IsStaging() ? "Beta" : "Produccion";
        var resultado = await consultarTicketCasoDeUso.EjecutarAsync(idInquilino, idLoteDocumento, ambienteCodigo, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    private IActionResult ResponderSegunEnvelope<T>(ResultadoOperacion<T> resultado) => resultado.IdTipoMensaje switch
    {
        TipoMensaje.Exito => Ok(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje, Datos = resultado.Datos }),
        TipoMensaje.ReglaDeNegocio => BadRequest(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje })
    };
}
