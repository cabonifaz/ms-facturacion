using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.Certificados;

namespace ms_facturacion.Controllers;

public sealed record InsertarCertificadoPeticion(
    int IdInquilino, int IdEmpresa, string RutaAlmacenamiento, string Sujeto, string Emisor,
    string NumeroSerie, string HuellaDigital, DateOnly ValidoDesde, DateOnly ValidoHasta, bool Activo);

public sealed record ActualizarCertificadoPeticion(
    string RutaAlmacenamiento, string Sujeto, string Emisor, string NumeroSerie, string HuellaDigital,
    DateOnly ValidoDesde, DateOnly ValidoHasta, bool Activo);

[ApiController]
[Route("api/v1/certificados")]
public sealed class CertificadosController(
    InsertarCertificadoCasoDeUso insertarCasoDeUso,
    ObtenerCertificadoCasoDeUso obtenerCasoDeUso,
    ListarCertificadosCasoDeUso listarCasoDeUso,
    ActualizarCertificadoCasoDeUso actualizarCasoDeUso,
    EliminarCertificadoCasoDeUso eliminarCasoDeUso) : ControllerBase
{
    // TODO: reemplazar por el usuario ejecutor real una vez definida la autenticación servicio-a-servicio con maximlian3_backend.
    private const string UsuarioEjecutor = "ms-facturacion";

    [HttpPost]
    public async Task<IActionResult> Insertar(InsertarCertificadoPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await insertarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, peticion.IdInquilino, peticion.IdEmpresa, peticion.RutaAlmacenamiento, peticion.Sujeto,
            peticion.Emisor, peticion.NumeroSerie, peticion.HuellaDigital, peticion.ValidoDesde, peticion.ValidoHasta,
            peticion.Activo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet("{idCertificado:int}")]
    public async Task<IActionResult> Obtener([FromQuery] int idInquilino, int idCertificado, CancellationToken cancellationToken)
    {
        var resultado = await obtenerCasoDeUso.EjecutarAsync(idInquilino, idCertificado, cancellationToken);
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

    [HttpPut("{idCertificado:int}")]
    public async Task<IActionResult> Actualizar(
        [FromQuery] int idInquilino, int idCertificado, ActualizarCertificadoPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await actualizarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, idCertificado, peticion.RutaAlmacenamiento, peticion.Sujeto, peticion.Emisor,
            peticion.NumeroSerie, peticion.HuellaDigital, peticion.ValidoDesde, peticion.ValidoHasta, peticion.Activo,
            cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpDelete("{idCertificado:int}")]
    public async Task<IActionResult> Eliminar([FromQuery] int idInquilino, int idCertificado, CancellationToken cancellationToken)
    {
        var resultado = await eliminarCasoDeUso.EjecutarAsync(UsuarioEjecutor, idInquilino, idCertificado, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    private IActionResult ResponderSegunEnvelope<T>(ResultadoOperacion<T> resultado) => resultado.IdTipoMensaje switch
    {
        TipoMensaje.Exito => Ok(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje, Datos = resultado.Datos }),
        TipoMensaje.ReglaDeNegocio => BadRequest(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje })
    };
}
