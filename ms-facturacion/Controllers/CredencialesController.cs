using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.Credenciales;

namespace ms_facturacion.Controllers;

// El valor sensible (ClaveSol, etc.) viaja en texto plano SOLO en este payload de entrada, sobre HTTPS,
// desde maximlian3_backend como puente de confianza — nunca vuelve a salir de este microservicio ni en
// claro ni cifrado: Obtener/Listar solo devuelven metadatos (ver CredencialInquilinoResumen).
public sealed record InsertarCredencialPeticion(
    int IdInquilino, int IdEmpresa, string TipoCredencialCodigo, string Usuario, string ValorPlano, bool Activo);

public sealed record ActualizarCredencialPeticion(string Usuario, string ValorPlano, bool Activo);

[ApiController]
[Route("api/v1/credenciales")]
public sealed class CredencialesController(
    InsertarCredencialCasoDeUso insertarCasoDeUso,
    ObtenerCredencialCasoDeUso obtenerCasoDeUso,
    ListarCredencialesCasoDeUso listarCasoDeUso,
    ActualizarCredencialCasoDeUso actualizarCasoDeUso,
    EliminarCredencialCasoDeUso eliminarCasoDeUso) : ControllerBase
{
    // TODO: reemplazar por el usuario ejecutor real una vez definida la autenticación servicio-a-servicio con maximlian3_backend.
    private const string UsuarioEjecutor = "ms-facturacion";

    [HttpPost]
    public async Task<IActionResult> Insertar(InsertarCredencialPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await insertarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, peticion.IdInquilino, peticion.IdEmpresa, peticion.TipoCredencialCodigo,
            peticion.Usuario, peticion.ValorPlano, peticion.Activo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet("{idCredencialInquilino:int}")]
    public async Task<IActionResult> Obtener(
        [FromQuery] int idInquilino, int idCredencialInquilino, CancellationToken cancellationToken)
    {
        var resultado = await obtenerCasoDeUso.EjecutarAsync(idInquilino, idCredencialInquilino, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int idInquilino, [FromQuery] int idEmpresa, [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20, CancellationToken cancellationToken = default)
    {
        var resultado = await listarCasoDeUso.EjecutarAsync(idInquilino, idEmpresa, pagina, tamanoPagina, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpPut("{idCredencialInquilino:int}")]
    public async Task<IActionResult> Actualizar(
        [FromQuery] int idInquilino, int idCredencialInquilino, ActualizarCredencialPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await actualizarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, idCredencialInquilino, peticion.Usuario, peticion.ValorPlano,
            peticion.Activo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpDelete("{idCredencialInquilino:int}")]
    public async Task<IActionResult> Eliminar(
        [FromQuery] int idInquilino, int idCredencialInquilino, CancellationToken cancellationToken)
    {
        var resultado = await eliminarCasoDeUso.EjecutarAsync(UsuarioEjecutor, idInquilino, idCredencialInquilino, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    private IActionResult ResponderSegunEnvelope<T>(ResultadoOperacion<T> resultado) => resultado.IdTipoMensaje switch
    {
        TipoMensaje.Exito => Ok(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje, Datos = resultado.Datos }),
        TipoMensaje.ReglaDeNegocio => BadRequest(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje })
    };
}
