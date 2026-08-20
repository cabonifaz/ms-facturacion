using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;
using ms_facturacion.Dominio;

namespace ms_facturacion.Controllers;

public sealed record ItemBajaPeticion(int IdDocumentoElectronico, string MotivoDescripcion);

public sealed record ComunicacionBajaPeticion(
    int IdInquilino, int IdEmpresa, DateOnly FechaReferencia, IReadOnlyList<ItemBajaPeticion> Items);

/// Mismo shape que ComunicacionBajaPeticion — la Boleta y sus Notas vinculadas van por Resumen Diario de
/// Baja (SP_LoteResumenBajaBoleta_Insertar) en vez de Comunicación de Baja.
public sealed record ResumenBajaBoletaPeticion(
    int IdInquilino, int IdEmpresa, DateOnly FechaReferencia, IReadOnlyList<ItemBajaPeticion> Items);

[ApiController]
[Route("api/v1/lotes-documento")]
public sealed class LotesDocumentoController(
    EnviarComunicacionBajaASunatCasoDeUso enviarBajaCasoDeUso,
    PrevisualizarBajaCasoDeUso previsualizarBajaCasoDeUso,
    ConsultarTicketComunicacionBajaCasoDeUso consultarTicketCasoDeUso,
    EnviarResumenBajaBoletaASunatCasoDeUso enviarResumenBajaBoletaCasoDeUso,
    PrevisualizarResumenBajaBoletaCasoDeUso previsualizarResumenBajaBoletaCasoDeUso,
    ConsultarTicketResumenBajaCasoDeUso consultarTicketResumenBajaCasoDeUso,
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
    // MotivoDescripcion por ítem ni FechaReferencia (a diferencia de ComunicacionBajaPeticion) — ninguno de
    // los dos hace falta para la validación (MotivoDescripcion nunca se lee; el chequeo de fecha compartida
    // compara las FechaEmision de los documentos entre sí, no contra un valor mandado por el llamador), así
    // que el payload que queda es solo escalares + una lista de ids, entra entero en query string.
    [HttpGet("comunicacion-baja/preview")]
    public async Task<IActionResult> PrevisualizarComunicacionBaja(
        [FromQuery] int idInquilino, [FromQuery] int idEmpresa,
        [FromQuery] IReadOnlyList<int> idsDocumentoElectronico, CancellationToken cancellationToken)
    {
        var resultado = await previsualizarBajaCasoDeUso.EjecutarAsync(idInquilino, idEmpresa, idsDocumentoElectronico, cancellationToken);

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

    // Resumen Diario de Baja de Boletas — mismo shape/criterio que ComunicacionBaja, para Boleta y sus
    // Notas vinculadas (SUNAT exige este mecanismo distinto para anularlas, no Comunicación de Baja).
    [HttpPost("resumen-baja-boleta")]
    public async Task<IActionResult> ResumenBajaBoleta(ResumenBajaBoletaPeticion peticion, CancellationToken cancellationToken)
    {
        var items = peticion.Items
            .Select(item => new ItemBajaEntrada(item.IdDocumentoElectronico, item.MotivoDescripcion))
            .ToList();

        var ambienteCodigo = entorno.IsDevelopment() || entorno.IsStaging() ? "Beta" : "Produccion";
        var resultado = await enviarResumenBajaBoletaCasoDeUso.EjecutarAsync(
            peticion.IdInquilino, peticion.IdEmpresa, peticion.FechaReferencia, items, ambienteCodigo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet("resumen-baja-boleta/preview")]
    public async Task<IActionResult> PrevisualizarResumenBajaBoleta(
        [FromQuery] int idInquilino, [FromQuery] int idEmpresa,
        [FromQuery] IReadOnlyList<int> idsDocumentoElectronico, CancellationToken cancellationToken)
    {
        var resultado = await previsualizarResumenBajaBoletaCasoDeUso.EjecutarAsync(idInquilino, idEmpresa, idsDocumentoElectronico, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    // Ruta separada de {idLoteDocumento}/consultar-ticket en vez de despacho genérico por TipoLoteCodigo —
    // evita un ObtenerAsync extra solo para decidir a qué caso de uso llamar, mismo criterio de simplicidad
    // que el resto de las rutas de este controller.
    [HttpPost("resumen-baja-boleta/{idLoteDocumento:int}/consultar-ticket")]
    public async Task<IActionResult> ConsultarTicketResumenBajaBoleta(
        [FromQuery] int idInquilino, int idLoteDocumento, CancellationToken cancellationToken)
    {
        var ambienteCodigo = entorno.IsDevelopment() || entorno.IsStaging() ? "Beta" : "Produccion";
        var resultado = await consultarTicketResumenBajaCasoDeUso.EjecutarAsync(idInquilino, idLoteDocumento, ambienteCodigo, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    private IActionResult ResponderSegunEnvelope<T>(ResultadoOperacion<T> resultado) => resultado.IdTipoMensaje switch
    {
        TipoMensaje.Exito => Ok(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje, Datos = resultado.Datos }),
        TipoMensaje.ReglaDeNegocio => BadRequest(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje })
    };
}
