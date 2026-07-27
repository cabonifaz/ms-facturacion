using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.Clientes;

namespace ms_facturacion.Controllers;

/// TipoDocumentoCodigo = Num1 de TABLA_MAESTRA IdMaestro=3 (código SUNAT de tipo de documento, ej. '6'=RUC),
/// PaisCodigo = Num1 de TABLA_MAESTRA IdMaestro=2 (misma numeración que ya usa maximlian3_backend para
/// identificar países) — ambos se matchean por id, no por string.
public sealed record InsertarClientePeticion(
    int IdInquilino, int TipoDocumentoCodigo, string NumeroDocumento, string Nombre,
    string? Correo, string? Direccion, int PaisCodigo);

public sealed record ActualizarClientePeticion(
    int TipoDocumentoCodigo, string NumeroDocumento, string Nombre, string? Correo, string? Direccion, int PaisCodigo);

[ApiController]
[Route("api/v1/clientes")]
public sealed class ClientesController(
    InsertarClienteCasoDeUso insertarCasoDeUso,
    ObtenerClienteCasoDeUso obtenerCasoDeUso,
    ListarClientesCasoDeUso listarCasoDeUso,
    ActualizarClienteCasoDeUso actualizarCasoDeUso,
    EliminarClienteCasoDeUso eliminarCasoDeUso) : ControllerBase
{
    // TODO: reemplazar por el usuario ejecutor real una vez definida la autenticación servicio-a-servicio con maximlian3_backend.
    private const string UsuarioEjecutor = "ms-facturacion";

    [HttpPost]
    public async Task<IActionResult> Insertar(InsertarClientePeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await insertarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, peticion.IdInquilino, peticion.TipoDocumentoCodigo, peticion.NumeroDocumento,
            peticion.Nombre, peticion.Correo, peticion.Direccion, peticion.PaisCodigo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet("{idCliente:int}")]
    public async Task<IActionResult> Obtener(
        [FromQuery] int idInquilino, int idCliente, CancellationToken cancellationToken)
    {
        var resultado = await obtenerCasoDeUso.EjecutarAsync(idInquilino, idCliente, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int idInquilino, [FromQuery] string? busqueda, [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20, CancellationToken cancellationToken = default)
    {
        var resultado = await listarCasoDeUso.EjecutarAsync(idInquilino, busqueda, pagina, tamanoPagina, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpPut("{idCliente:int}")]
    public async Task<IActionResult> Actualizar(
        [FromQuery] int idInquilino, int idCliente, ActualizarClientePeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await actualizarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, idCliente, peticion.TipoDocumentoCodigo, peticion.NumeroDocumento,
            peticion.Nombre, peticion.Correo, peticion.Direccion, peticion.PaisCodigo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpDelete("{idCliente:int}")]
    public async Task<IActionResult> Eliminar(
        [FromQuery] int idInquilino, int idCliente, CancellationToken cancellationToken)
    {
        var resultado = await eliminarCasoDeUso.EjecutarAsync(UsuarioEjecutor, idInquilino, idCliente, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    private IActionResult ResponderSegunEnvelope<T>(ResultadoOperacion<T> resultado) => resultado.IdTipoMensaje switch
    {
        TipoMensaje.Exito => Ok(new { resultado.Mensaje, Datos = resultado.Datos }),
        TipoMensaje.ReglaDeNegocio => BadRequest(new { resultado.Mensaje }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { resultado.Mensaje })
    };
}
