using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.SeriesDocumento;

namespace ms_facturacion.Controllers;

// Deshabilitado — sin caller (ni maximlian3_backend ni ningún worker interno lo usa hoy: las series se
// resuelven internamente vía SP al insertar un documento, no por un llamado HTTP distinto). Comentado en
// vez de borrado para retomarlo en un sprint futuro sin tener que reescribirlo.
/*
public sealed record InsertarSerieDocumentoPeticion(
    int IdInquilino, int IdEmpresa, int IdTipoDocumentoMaestro, string Serie, int NumeroActual, bool Activo);

public sealed record ActualizarSerieDocumentoPeticion(
    int IdTipoDocumentoMaestro, string Serie, int NumeroActual, bool Activo);

[ApiController]
[Route("api/v1/series-documento")]
public sealed class SeriesDocumentoController(
    InsertarSerieDocumentoCasoDeUso insertarCasoDeUso,
    ObtenerSerieDocumentoCasoDeUso obtenerCasoDeUso,
    ListarSeriesDocumentoCasoDeUso listarCasoDeUso,
    ActualizarSerieDocumentoCasoDeUso actualizarCasoDeUso,
    EliminarSerieDocumentoCasoDeUso eliminarCasoDeUso) : ControllerBase
{
    // TODO: reemplazar por el usuario ejecutor real una vez definida la autenticación servicio-a-servicio con maximlian3_backend.
    private const string UsuarioEjecutor = "ms-facturacion";

    [HttpPost]
    public async Task<IActionResult> Insertar(InsertarSerieDocumentoPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await insertarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, peticion.IdInquilino, peticion.IdEmpresa, peticion.IdTipoDocumentoMaestro,
            peticion.Serie, peticion.NumeroActual, peticion.Activo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet("{idSerieDocumento:int}")]
    public async Task<IActionResult> Obtener(
        [FromQuery] int idInquilino, int idSerieDocumento, CancellationToken cancellationToken)
    {
        var resultado = await obtenerCasoDeUso.EjecutarAsync(idInquilino, idSerieDocumento, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int idInquilino, [FromQuery] int idEmpresa, [FromQuery] string? busqueda,
        [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 20, CancellationToken cancellationToken = default)
    {
        var resultado = await listarCasoDeUso.EjecutarAsync(idInquilino, idEmpresa, busqueda, pagina, tamanoPagina, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpPut("{idSerieDocumento:int}")]
    public async Task<IActionResult> Actualizar(
        [FromQuery] int idInquilino, int idSerieDocumento, ActualizarSerieDocumentoPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await actualizarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, idSerieDocumento, peticion.IdTipoDocumentoMaestro,
            peticion.Serie, peticion.NumeroActual, peticion.Activo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpDelete("{idSerieDocumento:int}")]
    public async Task<IActionResult> Eliminar(
        [FromQuery] int idInquilino, int idSerieDocumento, CancellationToken cancellationToken)
    {
        var resultado = await eliminarCasoDeUso.EjecutarAsync(UsuarioEjecutor, idInquilino, idSerieDocumento, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    private IActionResult ResponderSegunEnvelope<T>(ResultadoOperacion<T> resultado) => resultado.IdTipoMensaje switch
    {
        TipoMensaje.Exito => Ok(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje, Datos = resultado.Datos }),
        TipoMensaje.ReglaDeNegocio => BadRequest(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje })
    };
}
*/
