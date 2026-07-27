using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.Inquilinos;

namespace ms_facturacion.Controllers;

public sealed record InsertarInquilinoPeticion(string Codigo, string Nombre, bool Activo);
public sealed record ActualizarInquilinoPeticion(string Codigo, string Nombre, bool Activo);

[ApiController]
[Route("api/v1/inquilinos")]
public sealed class InquilinosController(
    InsertarInquilinoCasoDeUso insertarCasoDeUso,
    ObtenerInquilinoCasoDeUso obtenerCasoDeUso,
    ListarInquilinosCasoDeUso listarCasoDeUso,
    ActualizarInquilinoCasoDeUso actualizarCasoDeUso,
    EliminarInquilinoCasoDeUso eliminarCasoDeUso) : ControllerBase
{
    // TODO: reemplazar por el usuario ejecutor real una vez definida la autenticación servicio-a-servicio con maximlian3_backend.
    private const string UsuarioEjecutor = "ms-facturacion";

    [HttpPost]
    public async Task<IActionResult> Insertar(InsertarInquilinoPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await insertarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, peticion.Codigo, peticion.Nombre, peticion.Activo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet("{idInquilino:int}")]
    public async Task<IActionResult> Obtener(int idInquilino, CancellationToken cancellationToken)
    {
        var resultado = await obtenerCasoDeUso.EjecutarAsync(idInquilino, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? busqueda, [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 20,
        CancellationToken cancellationToken = default)
    {
        var resultado = await listarCasoDeUso.EjecutarAsync(busqueda, pagina, tamanoPagina, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpPut("{idInquilino:int}")]
    public async Task<IActionResult> Actualizar(int idInquilino, ActualizarInquilinoPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await actualizarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, peticion.Codigo, peticion.Nombre, peticion.Activo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpDelete("{idInquilino:int}")]
    public async Task<IActionResult> Eliminar(int idInquilino, CancellationToken cancellationToken)
    {
        var resultado = await eliminarCasoDeUso.EjecutarAsync(UsuarioEjecutor, idInquilino, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    /// Traduce el envelope IdTipoMensaje/Mensaje a códigos HTTP: 2=200, 1=400 (regla de negocio), 3=500 (error de sistema).
    private IActionResult ResponderSegunEnvelope<T>(ResultadoOperacion<T> resultado) => resultado.IdTipoMensaje switch
    {
        TipoMensaje.Exito => Ok(new { resultado.Mensaje, Datos = resultado.Datos }),
        TipoMensaje.ReglaDeNegocio => BadRequest(new { resultado.Mensaje }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { resultado.Mensaje })
    };
}
